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
using Barotrauma.LuaCs;
using Microsoft.CodeAnalysis;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis.CSharp;

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
    private readonly Action<AssemblyLoader> _onUnload;
    /// <summary>
    /// This lock is just to ensure that we do not load while disposing
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

    public FluentResults.Result<Assembly> LoadAssemblyFromFile(string assemblyFilePath, 
        ImmutableArray<string> additionalDependencyPaths)
    {
        // TODO: Include runtime error diagnostics from Github issue.
        throw new NotImplementedException();
    }

    public FluentResults.Result<Assembly> GetAssemblyByName(string assemblyName)
    {
        throw new NotImplementedException();
    }
    
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
