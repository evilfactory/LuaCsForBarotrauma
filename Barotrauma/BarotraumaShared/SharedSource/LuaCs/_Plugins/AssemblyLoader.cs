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
using Barotrauma.LuaCs.Events;
using Microsoft.CodeAnalysis;
using Basic.Reference.Assemblies;
using FluentResults;
using FluentResults.LuaCs;
using LightInject;
using Microsoft.CodeAnalysis.CSharp;
using OneOf;
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
    private readonly IAssemblyManagementService _assemblyManagementService;
    private readonly IEventService _eventService;
    private readonly Action<AssemblyLoader> _onUnload;
    /// <summary>
    /// This lock is just to ensure that we do not load while disposing
    /// </summary>
    private readonly ReaderWriterLockSlim _operationsLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly ConcurrentDictionary<string, AssemblyDependencyResolver> _dependencyResolvers = new();
    private readonly ConcurrentDictionary<AssemblyOrStringKey, AssemblyData> _loadedAssemblyData = new();
    
    private ThreadLocal<bool> _isResolving = new(static()=>false); // cyclic resolution exit

    #region PublicAPI

    public AssemblyLoader(IAssemblyManagementService assemblyManagementService, 
        IEventService eventService, 
        Guid id, string name, 
        bool isReferenceOnlyMode, Action<AssemblyLoader> onUnload) 
        : base(isCollectible: true, name: name)
    {
        _assemblyManagementService = assemblyManagementService;
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
                return res.WithError(new ExceptionalError(ex)
                    .WithMetadata(MetadataType.Sources, path));
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

        if (_loadedAssemblyData.ContainsKey(assemblyName))
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

        if (!compileWithInternalAccess)
        {
            typeof(CSharpCompilationOptions)
            .GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(compilationOptions, (uint)1 << 22);
        }

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
            var data = new AssemblyData(LoadFromStream(asmMemoryStream), asmMemoryStream.ToArray());
            _loadedAssemblyData[data.Assembly] = data;
            return new FluentResults.Result<Assembly>().WithSuccess($"Compiled assembly {assemblyName} successful.").WithValue(data.Assembly);
        }
        catch (Exception ex)
        {
            return new FluentResults.Result().WithError(new ExceptionalError(ex));
        }
    }

    public FluentResults.Result<Assembly> LoadAssemblyFromFile(string assemblyFilePath, 
        ImmutableArray<string> additionalDependencyPaths)
    {
        if (assemblyFilePath.IsNullOrWhiteSpace())
            return new FluentResults.Result<Assembly>().WithError(new Error($"The path provided is null!"));
        
        if (additionalDependencyPaths.Any())
        {
            var r = AddDependencyPaths(additionalDependencyPaths);
            if (!r.IsFailed)
            {
                // we have errors, loading may not work.
                return FluentResults.Result.Fail(new Error($"Failed to load dependency paths")
                        .WithMetadata(MetadataType.ExceptionObject, this)
                        .WithMetadata(MetadataType.RootObject, assemblyFilePath))
                    .WithErrors(r.Errors);
            }
        }

        string sanitizedFilePath = Path.GetFullPath(assemblyFilePath.CleanUpPath());
        string directoryKey = Path.GetDirectoryName(sanitizedFilePath);

        if (directoryKey is null)
        {
            return FluentResults.Result.Fail(new Error($"Unable to load assembly: bath file path: {assemblyFilePath}")
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, sanitizedFilePath));
        }

        try
        {
            var assembly = LoadFromAssemblyPath(sanitizedFilePath);
            _loadedAssemblyData[assembly] = new AssemblyData(assembly, sanitizedFilePath);
            return new Result<Assembly>().WithSuccess($"Loaded assembly'{assembly.GetName()}'").WithValue(assembly);
        }
        catch (ArgumentNullException ane)
        {
            return FluentResults.Result.Fail<Assembly>(new ExceptionalError(ane)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, assemblyFilePath)
                .WithMetadata(MetadataType.ExceptionDetails, ane.Message)
                .WithMetadata(MetadataType.StackTrace, ane.StackTrace));
        }
        catch (ArgumentException ae)
        {
            return FluentResults.Result.Fail<Assembly>(new ExceptionalError(ae)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, assemblyFilePath)
                .WithMetadata(MetadataType.ExceptionDetails, ae.Message)
                .WithMetadata(MetadataType.StackTrace, ae.StackTrace));
        }
        catch (FileLoadException fle)
        {
            return FluentResults.Result.Fail<Assembly>(new ExceptionalError(fle)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, assemblyFilePath)
                .WithMetadata(MetadataType.ExceptionDetails, fle.Message)
                .WithMetadata(MetadataType.StackTrace, fle.StackTrace));
        }
        catch (FileNotFoundException fnfe)
        {
            return FluentResults.Result.Fail<Assembly>(new ExceptionalError(fnfe)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, assemblyFilePath)
                .WithMetadata(MetadataType.ExceptionDetails, fnfe.Message)
                .WithMetadata(MetadataType.StackTrace, fnfe.StackTrace));
        }
        catch (BadImageFormatException bife)
        {
            return FluentResults.Result.Fail<Assembly>(new ExceptionalError(bife)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, assemblyFilePath)
                .WithMetadata(MetadataType.ExceptionDetails, bife.Message)
                .WithMetadata(MetadataType.StackTrace, bife.StackTrace));
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail<Assembly>(new ExceptionalError(e)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, assemblyFilePath)
                .WithMetadata(MetadataType.ExceptionDetails, e.Message)
                .WithMetadata(MetadataType.StackTrace, e.StackTrace));
        }
    }

    public FluentResults.Result<Assembly> GetAssemblyByName(string assemblyName)
    {
        if (assemblyName.IsNullOrWhiteSpace())
        {
            return FluentResults.Result.Fail(new Error($"Assembly name is null")
                .WithMetadata(MetadataType.ExceptionObject, this));
        }

        if (_loadedAssemblyData.TryGetValue(assemblyName, out var data))
        {
            return new FluentResults.Result<Assembly>().WithSuccess(new Success($"Assembly found")).WithValue(data.Assembly);
        }

        foreach (var assembly1 in this.Assemblies.Where(a => !_loadedAssemblyData.ContainsKey(a)))
        {
            if (assembly1.GetName().FullName == assemblyName)
            {
                try
                {
                    if (!assembly1.Location.IsNullOrWhiteSpace())
                    {
                        _loadedAssemblyData[assembly1] = new AssemblyData(assembly1, assembly1.Location);
                    }
                    // we don't have the original byte array so we can't store it.
                }
                catch (NotSupportedException nse) // dynamic assembly or location property threw
                {
                    // ignored
                }

                return new FluentResults.Result<Assembly>().WithSuccess(new Success($"Assembly found")).WithValue(assembly1);
            }
        }

        return FluentResults.Result.Fail(new Error($"Assembly named { assemblyName } not found!"));
    }
    
    public FluentResults.Result<ImmutableArray<Type>> GetTypesInAssemblies()
    {
        try
        {
            return new FluentResults.Result<ImmutableArray<Type>>().WithValue(_loadedAssemblyData.SelectMany(kvp=> kvp.Value.Types).ToImmutableArray());
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(new ExceptionalError(e));
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Internals

    protected override Assembly Load(AssemblyName assemblyName)
    {
        if (_isResolving.Value)
            return null;
        
        _isResolving.Value = true;
        try
        {
            if (_loadedAssemblyData.TryGetValue(assemblyName.FullName, out var data))
                return data.Assembly;
            var idSpan = new[] { this.Id };
            if (_assemblyManagementService.GetLoadedAssembly(assemblyName, in idSpan) is { IsSuccess: true } ret)
                return ret.Value;
            return null;
        }
        catch (ArgumentNullException _)
        {
            return null;
        }
        finally
        {
            _isResolving.Value = false;
        }
    }

    // Use the default import resolver since native libraries are niche and not blocking for unloading.
    // Implement if conflicts become an issue.
    /*protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // Implement NativeLibrary::InternalLoadUnmanagedDll()
        throw new NotImplementedException();
    }*/
    
    private void OnUnload(AssemblyLoadContext context)
    {
        base.Unloading -= OnUnload;
        var wf = new WeakReference<IAssemblyLoaderService>(this);
        _eventService.PublishEvent<IEventAssemblyContextUnloading>((sub) => sub.OnAssemblyUnloading(wf));
        _onUnload?.Invoke(this);
        this.Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (ModUtils.Threading.CheckClearAndSetBool(ref _isDisposed))
        {
            _operationsLock.EnterWriteLock();
            try
            {
                _loadedAssemblyData.Clear();
                
            }
            finally
            {
                _operationsLock.ExitWriteLock();
            }
        }
    }

    private readonly record struct AssemblyData
    {
        public readonly Assembly Assembly;
        public readonly OneOf<byte[], string> AssemblyImageOrPath;
        public readonly MetadataReference AssemblyReference;
        public readonly ImmutableArray<Type> Types;

        public AssemblyData(Assembly assembly, byte[] assemblyImage)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            AssemblyImageOrPath = assemblyImage ?? throw new ArgumentNullException(nameof(assemblyImage));
            AssemblyReference = MetadataReference.CreateFromImage(assemblyImage);
            Types = assembly.GetSafeTypes().ToImmutableArray();
        }

        public AssemblyData(Assembly assembly, string path)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            AssemblyImageOrPath = path ?? throw new ArgumentNullException(nameof(path));
            AssemblyReference = MetadataReference.CreateFromFile(path);
            Types = assembly.GetSafeTypes().ToImmutableArray();
        }
    }

    private readonly record struct AssemblyOrStringKey : IEquatable<AssemblyOrStringKey>, IEqualityComparer<AssemblyOrStringKey>
    {
        public Assembly Assembly { get; init; }
        public string AssemblyName { get; init; }
        public readonly int HashCode;

        public AssemblyOrStringKey(Assembly assembly)
        {
            if(assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            Assembly = assembly;
            AssemblyName = assembly.GetName().FullName;
            if (AssemblyName == null)
                throw new ArgumentNullException(nameof(AssemblyName));
            HashCode = AssemblyName.GetHashCode();
        }
        
        public AssemblyOrStringKey(string assemblyName)
        {
            if (assemblyName.IsNullOrWhiteSpace())
                throw new ArgumentNullException(nameof(assemblyName));
            Assembly = null;
            AssemblyName = assemblyName;
            HashCode = AssemblyName.GetHashCode();
        }

        public bool Equals(AssemblyOrStringKey x, AssemblyOrStringKey y)
        {
            if (x.Assembly is not null && y.Assembly is not null)
                return x.Assembly == y.Assembly;
            return x.AssemblyName == y.AssemblyName;
        }

        public int GetHashCode(AssemblyOrStringKey obj)
        {
            return obj.HashCode;
        }
        
        public static implicit operator AssemblyOrStringKey(Assembly assembly) => new AssemblyOrStringKey(assembly);
        public static implicit operator AssemblyOrStringKey(string name) => new AssemblyOrStringKey(name);
    }
    
    #endregion
}
