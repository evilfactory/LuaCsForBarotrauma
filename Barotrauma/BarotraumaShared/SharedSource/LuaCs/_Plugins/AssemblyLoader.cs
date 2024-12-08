using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using Barotrauma.LuaCs;
using Microsoft.CodeAnalysis;
using Basic.Reference.Assemblies;
using FluentResults;
using FluentResults.LuaCs;
using LightInject;
using Microsoft.CodeAnalysis.CSharp;
using Path = Barotrauma.IO.Path;

[assembly: InternalsVisibleTo(IAssemblyLoaderService.InternalsAwareAssemblyName)]

namespace Barotrauma.LuaCs.Services;
public sealed class AssemblyLoader : AssemblyLoadContext, IAssemblyLoaderService
{
    public Guid Id { get; init; }
    public bool IsReferenceOnlyMode { get; init; }
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }
    private int _isDisposed;
    
    //internal
    private readonly IPluginManagementService _pluginManagementService;
    private readonly IEventService _eventService;
    private readonly Action<AssemblyLoader> _onUnload;
    /// <summary>
    /// This lock is just to ensure that we do not load while disposing
    /// </summary>
    private readonly ReaderWriterLockSlim _operationsLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly ConcurrentDictionary<string, AssemblyDependencyResolver> _dependencyResolvers = new();
    private readonly ConcurrentDictionary<string, (Assembly, byte[])> _memoryCompiledAssemblies = new();
    private readonly ConcurrentDictionary<Assembly, ImmutableArray<MetadataReference>> _loadedAssemblyReferences = new();
    private readonly ConcurrentDictionary<Assembly, ImmutableArray<Type>> _typeCache = new();
    
    private ThreadLocal<bool> _isResolving = new(static()=>false); // cyclic resolution exit
    

    #region PublicAPI

    public AssemblyLoader(IPluginManagementService pluginManagementService, 
        IEventService eventService, 
        Guid id, string name, 
        bool isReferenceOnlyMode, Action<AssemblyLoader> onUnload) 
        : base(isCollectible: true, name: name)
    {
        _pluginManagementService = pluginManagementService;
        _eventService = eventService;
        Id = id;
        IsReferenceOnlyMode = isReferenceOnlyMode;
        _onUnload = onUnload;
        if (_onUnload is not null)
        {
            base.Unloading += OnUnload;
        }
    }

    public FluentResults.Result AddDependencyPaths(ImmutableArray<string> paths)
    {
        if (paths.Length == 0)
            return FluentResults.Result.Ok();
        var res = new FluentResults.Result();
        foreach (var path in paths)
        {
            try
            {
                var p = Path.GetFullPath(path.CleanUpPath());
                _dependencyResolvers[p] = new AssemblyDependencyResolver(p);
            }
            catch (Exception ex)
            {
                return res.WithError(new ExceptionalError(ex));
            }
        }
        return FluentResults.Result.Ok();
    }

    public FluentResults.Result<Assembly> CompileScriptAssembly(
        [NotNull] string assemblyName,
        bool compileWithInternalAccess,
        ImmutableArray<SyntaxTree> syntaxTrees,
        ImmutableArray<MetadataReference> metadataReferences,
        CSharpCompilationOptions compilationOptions = null)
    {
        if (assemblyName.IsNullOrWhiteSpace())
        {
            return new FluentResults.Result<Assembly>().WithError(new Error($"The name provided is null!")
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, syntaxTrees));
        }

        if (_memoryCompiledAssemblies.ContainsKey(assemblyName))
        {
            return new FluentResults.Result<Assembly>().WithError(new Error($"The name provided is already assigned to an assembly!")
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, syntaxTrees));
        }
        
        var compilationAssemblyName = compileWithInternalAccess ? IAssemblyLoaderService.InternalsAwareAssemblyName : assemblyName;
        
        compilationOptions ??= new CSharpCompilationOptions(
            outputKind: OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            concurrentBuild: true,
            reportSuppressedDiagnostics: true,
            allowUnsafe: true);

        typeof(CSharpCompilationOptions)
            .GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(compilationOptions, (uint)1 << 22);

        using var asmMemoryStream = new MemoryStream();
        var result = CSharpCompilation.Create(compilationAssemblyName, syntaxTrees, metadataReferences, compilationOptions).Emit(asmMemoryStream);
        if (!result.Success)
        {
            var res = new FluentResults.Result().WithError(
                new Error($"Compilation failed for assembly {assemblyName}!"));
            var failuresDiag = result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error);
            foreach (var diag in failuresDiag)
            {
                res = res.WithError(new Error(diag.GetMessage())
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.ExceptionDetails, diag.Descriptor.Description));
            }
            return res;
        }
        
        asmMemoryStream.Seek(0, SeekOrigin.Begin);
        try
        {
            var assembly = LoadFromStream(asmMemoryStream);
            var assemblyImage = asmMemoryStream.ToArray();
            _memoryCompiledAssemblies[assemblyName] = (assembly, assemblyImage);
            _typeCache[assembly] = assembly.GetSafeTypes().ToImmutableArray();
            return new FluentResults.Result<Assembly>().WithSuccess($"Compiled assembly {assemblyName} successful.").WithValue(assembly);
        }
        catch (Exception ex)
        {
            return new FluentResults.Result().WithError(new ExceptionalError(ex));
        }
    }

    public FluentResults.Result<Assembly> LoadAssemblyFromFile(string assemblyFilePath, 
        ImmutableArray<string> additionalDependencyPaths)
    {
        // TODO: Include runtime error diagnostics from Github issue.
        throw new NotImplementedException();
    }

    public FluentResults.Result<Assembly> GetAssemblyByName(string assemblyName)
    {
        if (assemblyName.IsNullOrWhiteSpace())
        {
            return FluentResults.Result.Fail(new Error($"Assembly name is null")
                .WithMetadata(MetadataType.ExceptionObject, this));
        }

        if (_memoryCompiledAssemblies.TryGetValue(assemblyName, out var assembly))
        {
            return new FluentResults.Result<Assembly>().WithSuccess(new Success($"Assembly found")).WithValue(assembly.Item1);
        }

        foreach (var assembly1 in Assemblies)
        {
            if (assembly1.GetName().Name == assemblyName)
            {
                return new FluentResults.Result<Assembly>().WithSuccess(new Success($"Assembly found")).WithValue(assembly1);
            }
        }

        return FluentResults.Result.Fail(new Error($"Assembly named { assemblyName } not found!"));
    }
    
    public FluentResults.Result<ImmutableArray<Type>> GetTypesInAssemblies()
    {
        try
        {
            return new FluentResults.Result<ImmutableArray<Type>>().WithValue([
                .._typeCache.SelectMany(kvp => kvp.Value)
            ]);
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(new ExceptionalError(e));
        }
    }

    #endregion

    #region Internals

    protected override Assembly Load(AssemblyName assemblyName)
    {
        throw new NotImplementedException();
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // TODO: Implement NativeLibrary::InternalLoadUnmanagedDll()
        throw new NotImplementedException();
    }
    
    private void OnUnload(AssemblyLoadContext context)
    {
        base.Unloading -= OnUnload;
        _onUnload?.Invoke(this);
        this.Dispose(true);

        throw new NotImplementedException();
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (ModUtils.Threading.CheckClearAndSetBool(ref _isDisposed))
        {
            _operationsLock.EnterWriteLock();
            try
            {
                
            }
            finally
            {
                _operationsLock.ExitWriteLock();
            }
        }
    }
}
