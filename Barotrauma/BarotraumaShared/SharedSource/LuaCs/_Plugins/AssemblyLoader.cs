using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using Barotrauma.Extensions;
using Barotrauma.LuaCs;
using Microsoft.CodeAnalysis;
using FluentResults;
using FluentResults.LuaCs;
using Microsoft.CodeAnalysis.CSharp;
using OneOf;
using Path = System.IO.Path;

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

    /// <summary>
    /// This bool-int wrapper increments/decrements when set as true/false respectively and return true if the value > 0.
    /// </summary>
    private bool AreOperationRunning
    {
        get => Interlocked.CompareExchange(ref _operationsRunning, 0, 0) > 0;
        set // we use the set as our inc/decr 
        {
            if (value)
            {
                Interlocked.Add(ref _operationsRunning, 1);
            }
            else
            {
                Interlocked.Add(ref _operationsRunning, -1);
            }
        }
    }
    private int _operationsRunning;
    
    //internal
    private readonly IAssemblyManagementService _assemblyManagementService;
    private readonly Action<AssemblyLoader> _onUnload;
    private readonly Func<AssemblyLoadContext, AssemblyName, Assembly> _onResolvingManaged;
    private readonly Func<Assembly, string, IntPtr> _onResolvingUnmanagedDll;
    private readonly ConcurrentDictionary<string, AssemblyDependencyResolver> _dependencyResolvers = new();
    private readonly ConcurrentDictionary<AssemblyOrStringKey, AssemblyData> _loadedAssemblyData = new();
    
    private readonly ThreadLocal<bool> _isResolving = new(static()=>false); // cyclic resolution exit

    public AssemblyLoader(
        IAssemblyManagementService assemblyManagementService, 
        Guid id, string name, 
        bool isReferenceOnlyMode, 
        Action<AssemblyLoader> onUnload = null) 
        : base(isCollectible: true, name: name)
    {
        _assemblyManagementService = assemblyManagementService;
        Id = id;
        IsReferenceOnlyMode = isReferenceOnlyMode;
        base.Unloading += OnUnload;
        base.Resolving += OnResolvingManagedAssembly;
        base.ResolvingUnmanagedDll += OnResolvingUnmanagedDll;
    }

    private IntPtr OnResolvingUnmanagedDll(Assembly assembly, string assemblyName)
    {
        throw new NotImplementedException();
    }

    private Assembly OnResolvingManagedAssembly(AssemblyLoadContext assemblyLoadContext, AssemblyName assemblyName)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<MetadataReference> AssemblyReferences
    {
        get
        {
            if (IsDisposed || _loadedAssemblyData.IsEmpty)
                yield return null;
            AreOperationRunning = true;
            foreach (var data in _loadedAssemblyData.Values)
            {
                yield return data.AssemblyReference;
            }
            AreOperationRunning = false;
        }   
    }

    public FluentResults.Result AddDependencyPaths(ImmutableArray<string> paths)
    {
        if (IsDisposed)
            return FluentResults.Result.Fail($"Loader is disposed!");
        AreOperationRunning = true;
        try
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
        finally
        {
            AreOperationRunning = false;
        }
    }

    public FluentResults.Result<Assembly> CompileScriptAssembly(
        [NotNull] string assemblyName,
        bool compileWithInternalAccess,
        ImmutableArray<SyntaxTree> syntaxTrees,
        ImmutableArray<MetadataReference> metadataReferences,
        CSharpCompilationOptions compilationOptions = null)
    {
        if (IsDisposed)
            return FluentResults.Result.Fail($"Loader is disposed!");
        AreOperationRunning = true;
        try
        {
            if (assemblyName.IsNullOrWhiteSpace())
            {
                return new Result<Assembly>().WithError(new Error($"The name provided is null!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, syntaxTrees));
            }

            if (_loadedAssemblyData.ContainsKey(assemblyName))
            {
                return new Result<Assembly>().WithError(new Error($"The name provided is already assigned to an assembly!")
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
                    ?.SetValue(compilationOptions, 
                        (uint)1 << 25 // CSharp.BinderFlags.AllowAwaitInUnsafeContext
                        | (uint)1 << 22 // CSharp.BinderFlags.IgnoreAccessibility
                        | (uint)1 << 1 // CSharp.BinderFlags.SuppressObsoleteChecks
                    );
            }

            using var asmMemoryStream = new MemoryStream();
            var result = CSharpCompilation.Create(compilationAssemblyName, syntaxTrees, metadataReferences, compilationOptions).Emit(asmMemoryStream);
            if (!result.Success)
            {
                var res = new FluentResults.Result().WithError(
                    new Error($"Compilation failed for assembly {assemblyName}!")
                        .WithMetadata(MetadataType.ExceptionObject, this)
                        .WithMetadata(MetadataType.RootObject, syntaxTrees));
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
                return new Result<Assembly>().WithSuccess($"Compiled assembly {assemblyName} successful.").WithValue(data.Assembly);
            }
            catch (Exception ex)
            {
                return new FluentResults.Result().WithError(new ExceptionalError(ex));
            }
        }
        finally
        {
            AreOperationRunning = false;
        }
    }

    public FluentResults.Result<Assembly> LoadAssemblyFromFile(string assemblyFilePath, 
        ImmutableArray<string> additionalDependencyPaths)
    {
        if (IsDisposed)
            return FluentResults.Result.Fail($"Loader is disposed!");

        AreOperationRunning = true;
        try
        {
            if (assemblyFilePath.IsNullOrWhiteSpace())
                return new Result<Assembly>().WithError(new Error($"The path provided is null!"));
        
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
                /*var assembly = LoadFromAssemblyPath(sanitizedFilePath);
                _loadedAssemblyData[assembly] = new AssemblyData(assembly, sanitizedFilePath);
                return new Result<Assembly>().WithSuccess($"Loaded assembly'{assembly.GetName()}'").WithValue(assembly);*/
                
                // TODO: Load assembly using dependency resolvers.
                throw new NotImplementedException();
            }
            catch (ArgumentNullException ane)
            {
                return GenerateExceptionReturn(ane);
            }
            catch (ArgumentException ae)
            {
                return GenerateExceptionReturn(ae);
            }
            catch (FileLoadException fle)
            {
                return GenerateExceptionReturn(fle);
            }
            catch (FileNotFoundException fnfe)
            {
                return GenerateExceptionReturn(fnfe);
            }
            catch (BadImageFormatException bife)
            {
                return GenerateExceptionReturn(bife);
            }
            catch (Exception e)
            {
                return GenerateExceptionReturn(e);
            }
        }
        finally
        {
            AreOperationRunning = false;
        }

        FluentResults.Result<Assembly> GenerateExceptionReturn<T>(T exception) where T : Exception
        {
            return FluentResults.Result.Fail<Assembly>(new ExceptionalError(exception)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, assemblyFilePath)
                .WithMetadata(MetadataType.ExceptionDetails, exception.Message)
                .WithMetadata(MetadataType.StackTrace, exception.StackTrace));
        }
    }

    public FluentResults.Result<Assembly> GetAssemblyByName(string assemblyName)
    {
        if (IsDisposed)
            return FluentResults.Result.Fail(new Error($"Loader is disposed!"));
        if (assemblyName.IsNullOrWhiteSpace())
        {
            return FluentResults.Result.Fail(new Error($"Assembly name is null")
                .WithMetadata(MetadataType.ExceptionObject, this));
        }
        AreOperationRunning = true;
        try
        {
            if (_loadedAssemblyData.TryGetValue(assemblyName, out var data))
            {
                return new Result<Assembly>().WithSuccess(new Success($"Assembly found")).WithValue(data.Assembly);
            }

            // search any assemblies that were background loaded and we're unaware of.
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

                    return new Result<Assembly>().WithSuccess(new Success($"Assembly found")).WithValue(assembly1);
                }
            }

            return FluentResults.Result.Fail(new Error($"Assembly named { assemblyName } not found!"));
        }
        finally
        {
            AreOperationRunning = false;
        }
    }
    
    public FluentResults.Result<ImmutableArray<Type>> GetTypesInAssemblies()
    {
        if (IsDisposed)
            return FluentResults.Result.Fail(new Error($"Loader is disposed!"));
        AreOperationRunning = true;
        try
        {
            return new FluentResults.Result<ImmutableArray<Type>>().WithValue(_loadedAssemblyData
                .SelectMany(kvp => kvp.Value.Types).ToImmutableArray());
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(new ExceptionalError(e));
        }
        finally
        {
            AreOperationRunning = false;
        }
    }

    public IEnumerable<Type> UnsafeGetTypesInAssemblies()
    {
        if (IsDisposed)
            yield return null;
        AreOperationRunning = true;
        try
        {
            if (_loadedAssemblyData.None())
            {
                yield return null;
            }
            else
            {
                foreach (var assemblyData in _loadedAssemblyData.Values)
                {
                    foreach (var type in assemblyData.Types)
                    {
                        yield return type;
                    }
                }
            }
        }
        finally
        {
            AreOperationRunning = false;
        }
    }

    public Result<Type> GetTypeInAssemblies(string typeName)
    {
        if (IsDisposed)
            return FluentResults.Result.Fail(new Error($"Loader is disposed!"));
        AreOperationRunning = true;
        try
        {
            if (_loadedAssemblyData.IsEmpty)
                return FluentResults.Result.Fail(new Error($"No assemblies loaded!"));
            foreach (var assemblyData in _loadedAssemblyData)
            {
                if (assemblyData.Value.TypesByName.TryGetValue(typeName, out var type))
                    return new FluentResults.Result<Type>().WithSuccess($"Found type.").WithValue(type);
            }
            return FluentResults.Result.Fail(new Error($"No matching types found for { typeName }!"));
        }
        finally
        {
            AreOperationRunning = false;
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
            return; // we don't want to invoke events twice nor cause strong GC handles.
        IsDisposed = true;
        this.Unload();
        this.DisposeInternal();
    }

    ~AssemblyLoader()
    {
        this.DisposeInternal();
    }
    
    private void OnUnload(AssemblyLoadContext context)
    {
        // Try to wait for loading ops on other threads if they happen to occur with a timeout.
        // This should be an edge, should it even occur.
        DateTime timeout = DateTime.Now.AddSeconds(2);
        while (timeout > DateTime.Now)
        {
            if (!AreOperationRunning)
                break;
            Thread.Sleep(1000/GameMain.CurrentUpdateRate+1);
        }
        
        var wf = new WeakReference<IAssemblyLoaderService>(this);
        _onUnload?.Invoke(this);
    }

    private void DisposeInternal()
    {
        IsDisposed = true;
        base.Resolving -= OnResolvingManagedAssembly;
        base.ResolvingUnmanagedDll -= OnResolvingUnmanagedDll;
        base.Unloading -= OnUnload;
        this._dependencyResolvers.Clear();
        this._loadedAssemblyData.Clear();
        GC.SuppressFinalize(this);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        if (_isResolving.Value)
            return null;
        
        _isResolving.Value = true;
        try
        {
            // TODO: Resolve against in-memory types.
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

    private readonly record struct AssemblyData
    {
        public readonly Assembly Assembly;
        public readonly OneOf<byte[], string> AssemblyImageOrPath;
        public readonly MetadataReference AssemblyReference;
        public readonly ImmutableArray<Type> Types;
        public readonly ImmutableDictionary<string, Type> TypesByName;

        public AssemblyData(Assembly assembly, byte[] assemblyImage)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            AssemblyImageOrPath = assemblyImage ?? throw new ArgumentNullException(nameof(assemblyImage));
            AssemblyReference = MetadataReference.CreateFromImage(assemblyImage);
            Types = assembly.GetSafeTypes().ToImmutableArray();
            TypesByName = Types.ToImmutableDictionary(type => type.FullName, type => type);
        }

        public AssemblyData(Assembly assembly, string path)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            AssemblyImageOrPath = path ?? throw new ArgumentNullException(nameof(path));
            AssemblyReference = MetadataReference.CreateFromFile(path);
            Types = assembly.GetSafeTypes().ToImmutableArray();
            TypesByName = Types.ToImmutableDictionary(type => type.FullName, type => type);
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
            return this.HashCode;
        }
        
        public static implicit operator AssemblyOrStringKey(Assembly assembly) => new AssemblyOrStringKey(assembly);
        public static implicit operator AssemblyOrStringKey(string name) => new AssemblyOrStringKey(name);
    }
}
