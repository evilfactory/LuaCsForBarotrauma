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
using Barotrauma.Extensions;
using Barotrauma.IO;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;
using FluentResults;
using FluentResults.LuaCs;
using ImpromptuInterface.Build;
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
            .AppendLine("using System.Reflection;")
            .AppendLine("using Barotrauma;")
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
        throw new NotImplementedException();
    }

    public bool IsDisposed { get; }
    public FluentResults.Result Reset()
    {
        return FluentResults.Result.Fail("Not implemented");
    }

    #endregion
    
    private IServicesProvider _serviceProvider;
    private IAssemblyLoaderService.IFactory _assemblyLoaderFactory;
    private IStorageService _storageService;
    private ILoggerService _logger;
    private readonly ConcurrentDictionary<ContentPackage, IAssemblyLoaderService> _assemblyLoaders = new();
    private readonly AsyncReaderWriterLock _operationsLock = new();
    
    public PluginManagementService(
        IServicesProvider serviceProvider, 
        IAssemblyLoaderService.IFactory assemblyLoaderFactory, 
        IStorageService storageService, 
        ILoggerService logger)
    {
        Guard.IsNotNull(serviceProvider, nameof(serviceProvider));
        _serviceProvider = serviceProvider;
        _assemblyLoaderFactory = assemblyLoaderFactory;
        _storageService = storageService;
        _logger = logger;
    }

    public Result<ImmutableArray<Type>> GetImplementingTypes<T>(bool includeInterfaces = false, bool includeAbstractTypes = false,
        bool includeDefaultContext = true)
    {
        throw new NotImplementedException();
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

    public ImmutableArray<Result<(Type, T)>> ActivateTypeInstances<T>(ImmutableArray<Type> types, bool serviceInjection = true,
        bool hostInstanceReference = false) where T : IDisposable
    {
        throw new NotImplementedException();
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
        }
        
        return result;
        
        // helper methods
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
            return Basic.Reference.Assemblies.Net80.References.All
                .Union(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(ass => !ass.Location.IsNullOrWhiteSpace())
                    .Select(ass => MetadataReference.CreateFromFile(ass.Location)));
        }
    }

    private IntPtr OnAssemblyLoaderResolvingUnmanaged(Assembly arg1, string arg2)
    {
        throw new NotImplementedException();
    }

    private Assembly OnAssemblyLoaderResolvingManaged(IAssemblyLoaderService arg1, AssemblyName arg2)
    {
        throw new NotImplementedException();
    }

    private void OnAssemblyLoaderUnloading(IAssemblyLoaderService loader)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result UnloadManagedAssemblies()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (_assemblyLoaders.Count == 0)
        {
            return FluentResults.Result.Ok();
        }

        foreach (var loaderService in _assemblyLoaders)
        {
            
        }

        throw new NotImplementedException();
    }

    public Result<Assembly> GetLoadedAssembly(OneOf<AssemblyName, string> assemblyName, in Guid[] excludedContexts)
    {
        throw new NotImplementedException();
    }
}
