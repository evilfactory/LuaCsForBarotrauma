using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using Barotrauma.LuaCs.Services;
using Microsoft.CodeAnalysis;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis.CSharp;

[assembly: InternalsVisibleTo(AssemblyLoader.InternalsAwareAssemblyName)]

namespace Barotrauma.LuaCs.Services;
public sealed class AssemblyLoader : AssemblyLoadContext, IDisposable
{
    //public
    public Guid Id { get; init; }
    /// <summary>
    /// Indicates that the assemblies in this load context are metadata references only and not
    /// intended for execution.
    /// </summary>
    public bool IsReferenceOnlyMode { get; init; }

    /// <summary>
    /// Indicates that Unload was called on this context and that all strong references to it should be discarded. 
    /// </summary>
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }
    private int _isDisposed;
    /// <summary>
    /// Runtime value of constant <see cref="InternalsAwareAssemblyName"/> for extensibility use.
    /// </summary>
    public static readonly string InternalsAccessAssemblyName = InternalsAwareAssemblyName;
    /// <summary>
    /// Name for all runtime-compiled assemblies requiring access to <c>internal</c> assembly components. <seealso cref="InternalsVisibleToAttribute"/>
    /// </summary>
    public const string InternalsAwareAssemblyName = "InternalsAwareAssembly";
    
    //internal
    private readonly IPluginManagementService _pluginManagementService;
    private readonly Action<AssemblyLoader> _onUnload;
    /// <summary>
    /// This lock is just to ensure that we do not  
    /// </summary>
    private readonly ReaderWriterLockSlim _operationsLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly ConcurrentDictionary<string, AssemblyDependencyResolver> _dependencyResolvers = new();
    
    private ThreadLocal<bool> _isResolving = new(static()=>false); // cyclic resolution exit
    

    #region PublicAPI

    public AssemblyLoader(IPluginManagementService pluginManagementService, Guid id, string name, 
        bool isReferenceOnlyMode, Action<AssemblyLoader> onUnload) 
        : base(isCollectible: true, name: name)
    {
        _pluginManagementService = pluginManagementService;
        Id = id;
        IsReferenceOnlyMode = isReferenceOnlyMode;
        _onUnload = onUnload;
        if (_onUnload is not null)
        {
            base.Unloading += OnUnload;
        }
    }

    /// <summary>
    /// Compiles the supplied syntaxtrees and options into an in-memory assembly image.
    /// Builds metadata from loaded assemblies, only supply your own if you have in-memory images not managed by the
    /// AssemblyManager class. 
    /// </summary>
    /// <param name="friendlyAssemblyName"><c>[NotNull]</c>Name reference of the assembly.
    /// <para><b>[IMPORTANT]</b> This is used to reference this assembly as the true name will be forced if
    /// publicized assemblies are not used (InternalsVisibleTo Attrib).</para>
    /// Must be supplied for in-memory assemblies.
    /// <para>Must be unique to all other assemblies explicitly loaded using this context.</para></param>
    /// <param name="compileWithInternalAccess">Forces the assembly name to <see cref="InternalsAccessAssemblyName"/> and grants access to <c>internal</c>.</param>
    /// <param name="assemblyInternalName">The real assembly name used in compilation.
    /// <para><b>[IMPORTANT]</b>Cannot be null or empty if <see cref="compileWithInternalAccess"/> is false.</para></param>
    /// <param name="syntaxTrees"><c>[NotNull]</c>Syntax trees to compile into the assembly.</param>
    /// <param name="externMetadataReferences">Metadata to be used for compilation.
    /// [IMPORTANT] This method builds metadata from loaded assemblies, only supply your own if you have in-memory
    /// images not managed by the AssemblyManager class.</param>
    /// <param name="compilationOptions"><c>[NotNull]</c>CSharp compilation options. This method automatically adds the 'IgnoreAccessChecks' property for compilation.</param>
    /// <param name="externFileAssemblyReferences">Additional assemblies located in the FileSystem to build metadata references from.
    /// Assemblies here will have duplicates by the same name that are currently loaded filtered out.</param>
    /// <returns>Success state of the operation.</returns>
    public FluentResults.Result<Assembly> CompileScriptAssembly(
        [NotNull] string friendlyAssemblyName,
        bool compileWithInternalAccess,
        string assemblyInternalName,
        [NotNull] IEnumerable<SyntaxTree> syntaxTrees,
        ImmutableArray<MetadataReference> externMetadataReferences,
        [NotNull] CSharpCompilationOptions compilationOptions,
        ImmutableArray<Assembly> externFileAssemblyReferences)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Loads the assembly from the provided location and registers all new paths provided with dependency resolution.
    /// </summary>
    /// <param name="assemblyFilePath">Absolute path to the managed assembly.</param>
    /// <param name="additionalDependencyPaths">Additional paths for dependency resolution.</param>
    /// <returns>Success and reference to the assembly if successful.</returns>
    public FluentResults.Result<Assembly> LoadAssemblyFromFile(string assemblyFilePath, 
        ImmutableArray<string> additionalDependencyPaths)
    {
        // TODO: Include runtime error diagnostics from Github issue.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the already loaded assembly with the same name.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly.</param>
    /// <returns></returns>
    public FluentResults.Result<Assembly> GetAssemblyByName(string assemblyName)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the list of <c>Type</c>s from loaded assemblies.
    /// </summary>
    /// <param name="includeReferenceExplicitOnly">Only include assemblies that were explicitly loaded and not automatically
    /// loaded dependencies.</param>
    /// <returns></returns>
    public FluentResults.Result<ImmutableArray<Type>> GetTypesInAssemblies(bool includeReferenceExplicitOnly = false)
    {
        throw new NotImplementedException();
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
