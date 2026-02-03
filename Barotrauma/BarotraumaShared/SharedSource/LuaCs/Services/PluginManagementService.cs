using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Barotrauma.Extensions;
using Barotrauma.IO;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;
using FluentResults;
using FluentResults.LuaCs;
using ImpromptuInterface.Build;
using LightInject;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Diagnostics;
using OneOf;

namespace Barotrauma.LuaCs.Services;

public class PluginManagementService : IAssemblyManagementService
{
    #region CSHARP_COMPILATION_OPTIONS

    private static readonly CSharpParseOptions ScriptParseOptions = CSharpParseOptions.Default
        .WithPreprocessorSymbols(new[]
        {
#if SERVER
            "SERVER"
#elif CLIENT
            "CLIENT"
#else
            "UNDEFINED"
#endif
#if DEBUG
            ,"DEBUG"
#endif
        });

#if WINDOWS
    private const string PLATFORM_TARGET = "Windows";
#elif OSX
    private const string PLATFORM_TARGET = "OSX";
#elif LINUX
    private const string PLATFORM_TARGET = "Linux";
#endif

#if CLIENT
    private const string ARCHITECTURE_TARGET = "Client";
#elif SERVER
    private const string ARCHITECTURE_TARGET = "Server";
#endif

    private static readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        .WithMetadataImportOptions(MetadataImportOptions.All)
#if DEBUG
        .WithOptimizationLevel(OptimizationLevel.Debug)
#else
        .WithOptimizationLevel(OptimizationLevel.Release)
#endif
        .WithAllowUnsafe(true);
    
    private static readonly SyntaxTree BaseAssemblyImports = CSharpSyntaxTree.ParseText(
        new StringBuilder()
            .AppendLine("global using LuaCsHook = Barotrauma.LuaCs.Services.Compatibility.ILuaCsHook;")
            .AppendLine("using System.Reflection;")
            .AppendLine("using Barotrauma;")
            .AppendLine("using Barotrauma.LuaCs;")
            .AppendLine("using Barotrauma.LuaCs.Services;")
            .AppendLine("using Barotrauma.LuaCs.Services.Compatibility;")
            .AppendLine("using System.Runtime.CompilerServices;")
            .AppendLine("[assembly: IgnoresAccessChecksTo(\"BarotraumaCore\")]")
#if CLIENT
            .AppendLine("[assembly: IgnoresAccessChecksTo(\"Barotrauma\")]")
#elif SERVER
            .AppendLine("[assembly: IgnoresAccessChecksTo(\"DedicatedServer\")]")
#endif
            .ToString(),
        ScriptParseOptions);

    #endregion
    
    #region Disposal

    public void Dispose()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
        {
            return;
        }

        UnsafeDisposeResourcesInternal();
        _assemblyLoaderFactory = null;
        _storageService = null;
        _eventService = null;
        _logger = null;
        _configService = null;
        _luaScriptManagementService = null;
        
        GC.SuppressFinalize(this);
    }

    private void UnsafeDisposeResourcesInternal()
    {
        foreach (var packPlugin in _pluginInstances.SelectMany(kvp => kvp.Value.Select(pluginInst => (kvp.Key, pluginInst))))
        {
            try
            {
                packPlugin.pluginInst.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError($"Error while disposing plugin for ContentPackage {packPlugin.Key.Name}: \n{e.Message}");
            }
        }
        _pluginInstances.Clear();
        _pluginInjectorContainer.Dispose();
        _pluginInjectorContainer = null;
        
        foreach (var loader in _assemblyLoaders)
        {
            try
            {
                loader.Value.Dispose();
                _unloadingAssemblyLoaders.Add(loader.Value, loader.Key);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to dispose of {nameof(IAssemblyLoaderService)} for ContentPackage {loader.Key.Name}: \n{e.Message}");
                if (loader.Value.Assemblies.Any())
                {
                    foreach (var ass in loader.Value.Assemblies)
                    {
                        _logger.LogWarning($"{nameof(PluginManagementService)}: Fallback manual unsubscription of assemblies: {ass.GetName()}");
                        ReflectionUtils.RemoveAssemblyFromCache(ass);
                    }
                }
            }
        }
        _assemblyLoaders.Clear();
    }

    private int _isDisposed = 0;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }
    public FluentResults.Result Reset()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        UnsafeDisposeResourcesInternal();
        return FluentResults.Result.Ok();
    }

    #endregion
    
    private IAssemblyLoaderService.IFactory _assemblyLoaderFactory;
    private IStorageService _storageService;
    private ILoggerService _logger;
    private Lazy<IEventService> _eventService;
    private Lazy<IConfigService> _configService;
    private Lazy<ILuaScriptManagementService> _luaScriptManagementService;
    private readonly ConcurrentDictionary<ContentPackage, IAssemblyLoaderService> _assemblyLoaders = new();
    private readonly ConcurrentDictionary<ContentPackage, ImmutableArray<IAssemblyPlugin>> _pluginInstances = new();
    private readonly ConditionalWeakTable<IAssemblyLoaderService, ContentPackage> _unloadingAssemblyLoaders = new();
    private readonly AsyncReaderWriterLock _operationsLock = new();
    private ServiceContainer _pluginInjectorContainer;
    
    public PluginManagementService(
        IAssemblyLoaderService.IFactory assemblyLoaderFactory, 
        IStorageService storageService, 
        ILoggerService logger, 
        Lazy<IEventService> eventService, 
        Lazy<ILuaScriptManagementService> luaScriptManagementService, 
        Lazy<IConfigService> configService)
    {
        _assemblyLoaderFactory = assemblyLoaderFactory;
        _storageService = storageService;
        _logger = logger;
        _eventService = eventService;
        _luaScriptManagementService = luaScriptManagementService;
        _configService = configService;
    }

    private ServiceContainer CreatePluginServiceContainer()
    {
        var container = new ServiceContainer(new ContainerOptions()
        {
            EnablePropertyInjection = true,
            
        });

        container.Register<ILoggerService>(fac => _logger);
        container.Register<IStorageService>(fac => _storageService);
        container.Register<IEventService>(fac => _eventService.Value);
        container.Register<ILuaScriptManagementService>(fac => _luaScriptManagementService.Value);
        container.Register<IConfigService>(fac => _configService.Value);

        return container;
    }

    public Result<ImmutableArray<Type>> GetImplementingTypes<T>(bool includeInterfaces = false, bool includeAbstractTypes = false,
        bool includeDefaultContext = true)
    {
#if !DEBUG
        throw new NotImplementedException();
#endif
        var builder = ImmutableArray.CreateBuilder<Type>();
        
        foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in ass.GetSafeTypes())
            {
                if ((includeInterfaces || !type.IsInterface)
                    && (includeAbstractTypes || !type.IsAbstract))
                {
                    builder.Add(type);
                }
            }
        }

        return builder.ToImmutable();
    }

    public Type GetType(string typeName, bool isByRefType = false, bool includeInterfaces = false,
        bool includeDefaultContext = true)
    {
        if (typeName.StartsWith("out ") || typeName.StartsWith("ref "))
        {
            typeName = typeName.Remove(0, 4);
            isByRefType = true;
        }

        if (includeDefaultContext)
        {
            var type = Type.GetType(typeName, false, false);
            if (type is not null && (includeInterfaces || !type.IsInterface))
            {
                if (isByRefType)
                {
                    return type.MakeByRefType();
                }

                return type;
            }
        }

        foreach (var ass in AssemblyLoadContext.All.SelectMany(alc => alc.Assemblies))
        {
            if (ass.GetType(typeName, false, false) is not {} type || (!includeInterfaces && type.IsInterface))
            {
                continue;
            }

            return isByRefType ? type.MakeByRefType() : type;
        }

        return null;
    }

    public FluentResults.Result ActivatePluginInstances(ImmutableArray<ContentPackage> executionOrder, bool excludeAlreadyRunningPackages = true)
    {
        if (executionOrder.IsDefaultOrEmpty)
        {
            ThrowHelper.ThrowArgumentNullException($"{nameof(ActivatePluginInstances)}: The ececution list provided is empty.");
        }
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (_assemblyLoaders.IsEmpty)
        {
            return FluentResults.Result.Ok();
        }

        var results = new FluentResults.Result();

        var toLoad = _assemblyLoaders
            .Where(al => executionOrder.Contains(al.Key))
            .Where(al => !excludeAlreadyRunningPackages || !_pluginInstances.ContainsKey(al.Key))
            .SelectMany(al => al.Value.Assemblies.Select(ass => (al.Key, ass)))
            .SelectMany(kvp => kvp.ass.GetSafeTypes()
                .Where(type =>
                    type is { IsInterface: false, IsAbstract: false, IsGenericType: false } 
                    && type.IsAssignableTo(typeof(IAssemblyPlugin)))
                .Select(type => (kvp.Key, type)))
            .GroupBy(kvp => kvp.Key, kvp => kvp.type)
            .OrderBy(exeGrp => executionOrder.IndexOf(exeGrp.Key))
            .ToImmutableArray();

        if (toLoad.Length == 0)
        {
            return FluentResults.Result.Ok();
        }
        
        var loadedPackagePlugins =
            ImmutableArray.CreateBuilder<(ContentPackage Package, ImmutableArray<IAssemblyPlugin> Plugins)>();
        _pluginInjectorContainer ??= CreatePluginServiceContainer();
        
        foreach (var packageTypes in toLoad)
        {
            var loadedTypes = ImmutableArray.CreateBuilder<IAssemblyPlugin>();
            foreach (var pluginType in packageTypes)
            {
                try
                {
                    var plugin = (IAssemblyPlugin)Activator.CreateInstance(pluginType);
                    _pluginInjectorContainer.InjectProperties(plugin);
                    _pluginInjectorContainer.Register(pluginType, fac => plugin);
                    loadedTypes.Add(plugin);
                }
                catch (Exception e)
                {
                    results.WithError(new ExceptionalError(e));
                    continue;
                }
            }
            loadedPackagePlugins.Add((packageTypes.Key, loadedTypes.ToImmutable()));
        }

        var packPluginGroups = loadedPackagePlugins.ToImmutable();
        foreach (var packagePluginGrp in packPluginGroups)
        {
            if (_pluginInstances.TryGetValue(packagePluginGrp.Package, out var plugins))
            {
                _pluginInstances[packagePluginGrp.Package] = plugins.Concat(packagePluginGrp.Plugins).ToImmutableArray();
                continue;
            }

            _pluginInstances[packagePluginGrp.Package] = packagePluginGrp.Plugins;
        }

        var pluginsToInit = packPluginGroups.SelectMany(ppg => ppg.Plugins).ToImmutableArray();

        foreach (var plugin in pluginsToInit)
        {
            results.WithReasons(PluginInitRunner(plugin, p => p.PreInitPatching()).Reasons);
        }

        _eventService.Value.PublishEvent<IEventPluginPreInitialize>(sub => sub.PreInitPatching());
        
        foreach (var plugin in pluginsToInit)
        {
            results.WithReasons(PluginInitRunner(plugin, p => p.Initialize()).Reasons);
        }
        
        _eventService.Value.PublishEvent<IEventPluginInitialize>(sub => sub.Initialize());
        
        foreach (var plugin in pluginsToInit)
        {
            results.WithReasons(PluginInitRunner(plugin, p => p.OnLoadCompleted()).Reasons);
        }

        _eventService.Value.PublishEvent<IEventPluginLoadCompleted>(sub => sub.OnLoadCompleted());

        return results;

        // helper
        FluentResults.Result PluginInitRunner(IAssemblyPlugin plugin, Action<IAssemblyPlugin> action)
        {
            try
            {
                action(plugin);
                return FluentResults.Result.Ok();
            }
            catch (Exception e)
            {
                return FluentResults.Result.Fail(new ExceptionalError(e));
            }
        }
    }


    public FluentResults.Result LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resources)
    {
        if (resources.IsDefaultOrEmpty)
        {
            ThrowHelper.ThrowArgumentNullException($"{nameof(LoadAssemblyResources)} The resource list is empty.)");
        }
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        var orderedContentPacks = resources.GroupBy(res => res.OwnerPackage)
            .OrderBy(res => resources.FindIndex(r2 => r2.OwnerPackage == res.Key))
            .ToImmutableArray();

        var result = new FluentResults.Result();
        
        foreach (var contentPack in orderedContentPacks)
        {
            LoadBinaries(contentPack);
            LoadAndCompileScriptAssemblies(contentPack);
            foreach (var ass in _assemblyLoaders[contentPack.Key].Assemblies)
            {
                ReflectionUtils.AddNonAbstractAssemblyTypes(ass);
            }
        }
        
        return result;
        
        // --- helper methods
        void LoadBinaries(IGrouping<ContentPackage,IAssemblyResourceInfo> contentPackRes)
        {
            var binaries = contentPackRes.Where(cRes => !cRes.IsScript)
                .OrderBy(bin => bin.LoadPriority)
                .SelectMany(bin => bin.FilePaths)
                .ToImmutableArray();

            if (binaries.IsDefaultOrEmpty)
            {
                return;
            }
            
            var assemblyLoader = _assemblyLoaders.GetOrAdd(contentPackRes.Key, (cp) => _assemblyLoaderFactory.CreateInstance(
                new IAssemblyLoaderService.LoaderInitData(
                    InstanceId: Guid.NewGuid(),
                    contentPackRes.Key.Name,
                    IsReferenceMode: false,
                    OwnerPackage: contentPackRes.Key,
                    OnUnload: OnAssemblyLoaderUnloading,
                    OnResolvingManaged: OnAssemblyLoaderResolvingManaged, 
                    OnResolvingUnmanagedDll: OnAssemblyLoaderResolvingUnmanaged
                )));

            var dependencyPaths = binaries
                .Select(bin => System.IO.Path.GetDirectoryName(bin.FullPath))
                .Distinct()
                .ToImmutableArray();
                
            foreach (var binResource in binaries)
            {
                var res = assemblyLoader.LoadAssemblyFromFile(binResource.FullPath, dependencyPaths);
                result.WithReasons(res.Reasons);
#if DEBUG
                _logger.LogResults(res.ToResult());
#endif
                if (res.IsFailed)
                {
                    _logger.LogResults(res.ToResult());
                }
            }
        }
        
        void LoadAndCompileScriptAssemblies(IGrouping<ContentPackage, IAssemblyResourceInfo> contentPackRes)
        {
            var scriptsGrp = contentPackRes.Where(cRes => cRes.IsScript)
                .Select(scr => (scr.OwnerPackage, scr.FriendlyName, scr.FilePaths, scr.UseInternalAccessName, scr.LoadPriority))
                .OrderBy(scr => scr.LoadPriority)
                .GroupBy(scr => scr.FriendlyName)
                .ToImmutableArray();

            if (scriptsGrp.IsDefaultOrEmpty)
            {
                return;
            }

            var metadataReferences = GetMetadataReferences();
            
            var assemblyLoader = _assemblyLoaders.GetOrAdd(contentPackRes.Key, (cp) => _assemblyLoaderFactory.CreateInstance(
                new IAssemblyLoaderService.LoaderInitData(
                    InstanceId: Guid.NewGuid(),
                    contentPackRes.Key.Name,
                    IsReferenceMode: false,
                    OwnerPackage: contentPackRes.Key,
                    OnUnload: OnAssemblyLoaderUnloading,
                    OnResolvingManaged: OnAssemblyLoaderResolvingManaged, 
                    OnResolvingUnmanagedDll: OnAssemblyLoaderResolvingUnmanaged
                )));
            
            // create syntax trees

            foreach (var scripts in scriptsGrp)
            {
                var syntaxTreesBuilder = ImmutableArray.CreateBuilder<SyntaxTree>();

                bool hasInternalsAwareBeenAdded = false;
                bool compileWithInternalName = true; 
                
                foreach (var resourceInfo in scripts)
                {
                    if (!hasInternalsAwareBeenAdded && resourceInfo.UseInternalAccessName)
                    {
                        hasInternalsAwareBeenAdded = true;
                        syntaxTreesBuilder.Add(BaseAssemblyImports);
                    }
                    
                    if (resourceInfo.FilePaths.IsDefaultOrEmpty)
                    {
                        ThrowHelper.ThrowArgumentNullException($"{nameof(LoadAndCompileScriptAssemblies)} The resource list is empty for package {resourceInfo.OwnerPackage}.");
                    }

                    foreach (var resourcePath in resourceInfo.FilePaths)
                    {
                        var loadRes = GetSourceFilesText(resourcePath);
                        if (loadRes.IsFailed)
                        {
                            _logger.LogResults(loadRes.ToResult());
                            continue;
                        }
                    
                        // this should be the same for the entire collection of src files so we just grab it from the collection
                        compileWithInternalName = resourceInfo.UseInternalAccessName;
                
                        CancellationToken token = CancellationToken.None;
                
                        syntaxTreesBuilder.Add(SyntaxFactory.ParseSyntaxTree(
                            text: loadRes.Value,
                            options: ScriptParseOptions,
                            path: null,
                            encoding: Encoding.Default,
                            cancellationToken: token
                        ));
                    }
                }

                if (syntaxTreesBuilder.Count < 1)
                {
                    continue;
                }
                
#if DEBUG
                _logger.Log($"[DEBUG] Compiling assembly for {scripts.Key}, in ContentPackage {contentPackRes.Key.Name}");
#endif
                
                result.WithReasons(assemblyLoader.CompileScriptAssembly(
                    assemblyName: scripts.Key,
                    compileWithInternalAccess: compileWithInternalName,
                    syntaxTrees: syntaxTreesBuilder.ToImmutable(),
                    metadataReferences: metadataReferences.ToImmutableArray(),
                    compilationOptions: CompilationOptions)
                    .Reasons);
            }
        }
        
        Result<string> GetSourceFilesText(ContentPath resourceInfoFilePath)
        {
            if (_storageService.LoadPackageText(resourceInfoFilePath) is not { IsFailed: false } res)
            {
                _logger.LogError($"{nameof(GetSourceFilesText)}: Failed to load source file for ContentPackage {resourceInfoFilePath.ContentPackage?.Name}.");
                return FluentResults.Result.Fail($"{nameof(GetSourceFilesText)}: Failed to load source files for ContentPackage {resourceInfoFilePath.ContentPackage?.Name}.");
            }

            return res;
        }
        
        IEnumerable<MetadataReference> GetMetadataReferences()
        {
#if !DEBUG
            throw new NotImplementedException($"Needs to use publicized barotrauma assemblies and cache metadata.");
#endif
            return Basic.Reference.Assemblies.Net80.References.All
                .Union(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(ass => !ass.Location.IsNullOrWhiteSpace())
                    .Select(ass => MetadataReference.CreateFromFile(ass.Location)));
        }
    }

    private IntPtr OnAssemblyLoaderResolvingUnmanaged(Assembly arg1, string arg2)
    {
        // TODO: Implement extern assembly lookup for Native/Unmanaged Assemblies.
        throw new NotImplementedException();
    }

    private Assembly OnAssemblyLoaderResolvingManaged(IAssemblyLoaderService requestingLoader, AssemblyName searchName)
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        foreach (var loader in _assemblyLoaders.Where(kvp => kvp.Value != requestingLoader)
                     .Select(kvp => kvp.Value).ToImmutableArray())
        {
            if (loader.IsReferenceOnlyMode || !loader.Assemblies.Any())
            {
                continue;
            }

            foreach (var assembly in loader.Assemblies)
            {
                if (assembly.GetName().Equals(searchName))
                {
                    return assembly;
                }
            }
        }

        return null;
    }

    private void OnAssemblyLoaderUnloading(IAssemblyLoaderService loader)
    {
        if (!loader.Assemblies.Any())
        {
            return;
        }

        foreach (var assembly in loader.Assemblies)
        {
            _eventService?.Value?.PublishEvent<IEventAssemblyUnloading>(sub => sub.OnAssemblyUnloading(assembly));
        }
    }

    public FluentResults.Result UnloadManagedAssemblies()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (_assemblyLoaders.Count == 0)
        {
            return FluentResults.Result.Ok();
        }
        
        var results = new FluentResults.Result();
        
        results.WithReasons(UnsafeDisposeManagedTypeInstances().Reasons);
        
        ReflectionUtils.ResetCache();
        foreach (var loaderService in _assemblyLoaders)
        {
            try
            {
                loaderService.Value.Dispose();
                _unloadingAssemblyLoaders.Add(loaderService.Value,  loaderService.Key);
            }
            catch (Exception e)
            {
                results.WithError(new ExceptionalError(e));
            }
        }

        _assemblyLoaders.Clear();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true);
        
        return results;
    }

    private FluentResults.Result UnsafeDisposeManagedTypeInstances()
    {
        var results = new FluentResults.Result();
        _pluginInjectorContainer = null;
        if (_pluginInstances.IsEmpty)
        {
            return FluentResults.Result.Ok();
        }

        foreach (var instance in _pluginInstances.SelectMany(kvp => kvp.Value))
        {
            try
            {
                instance.Dispose();
            }
            catch (Exception e)
            {
                results.WithError(new ExceptionalError(e));
                continue;
            }
        }
        
        _pluginInstances.Clear();
        
        return results;
    }

    public Result<Assembly> GetLoadedAssembly(OneOf<AssemblyName, string> assemblyName, in Guid[] excludedContexts)
    {
        throw new NotImplementedException();
    }
}
