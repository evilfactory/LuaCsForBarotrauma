using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
// ReSharper disable InconsistentNaming

namespace Barotrauma.LuaCs.Services;


public interface IAssemblyManagementService : IService
{
    #region Public API

    /// <summary>
    /// Called when an assembly is loaded.
    /// </summary>
    public event Action<Assembly> OnAssemblyLoaded;
    
    /// <summary>
    /// Called when an assembly is marked for unloading, before unloading begins. You should use this to cleanup
    /// any references that you have to this assembly.
    /// </summary>
    public event Action<Assembly> OnAssemblyUnloading; 
    
    /// <summary>
    /// Called whenever an exception is thrown. First arg is a formatted message, Second arg is the Exception.
    /// </summary>
    public event Action<string, Exception> OnException;

    /// <summary>
    /// For unloading issue debugging. Called whenever MemoryFileAssemblyContextLoader [load context] is unloaded. 
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public event Action<Guid> OnACLUnload;


    /// <summary>
    /// [DEBUG ONLY]
    /// Returns a list of the current unloading ACLs. 
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public ImmutableList<WeakReference<MemoryFileAssemblyContextLoader>> StillUnloadingACLs { get; }
    
    // ReSharper disable once MemberCanBePrivate.Global
    /// <summary>
    /// Checks if there are any AssemblyLoadContexts still in the process of unloading.
    /// </summary>
    public bool IsCurrentlyUnloading { get; }

    /// <summary>
    /// Allows iteration over all non-interface types in all loaded assemblies in the AsmMgr that are assignable to the given type (IsAssignableFrom).
    /// Warning: care should be used when using this method in hot paths as performance may be affected.
    /// </summary>
    /// <typeparam name="T">The type to compare against</typeparam>
    /// <param name="rebuildList">Forces caches to clear and for the lists of types to be rebuilt.</param>
    /// <returns>An Enumerator for matching types.</returns>
    public IEnumerable<Type> GetSubTypesInLoadedAssemblies<T>(bool rebuildList);

    /// <summary>
    /// Tries to get types assignable to type from the ACL given the Guid.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="types"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>Operation success.</returns>
    public bool TryGetSubTypesFromACL<T>(Guid id, out IEnumerable<Type> types);

    /// <summary>
    /// Tries to get types from the ACL given the Guid.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    public bool TryGetSubTypesFromACL(Guid id, out IEnumerable<Type> types);

    /// <summary>
    /// Allows iteration over all types, including interfaces, in all loaded assemblies in the AsmMgr who's names match the string.
    /// Note: Will return the by-reference equivalent type if the type name is prefixed with "out " or "ref ".
    /// </summary>
    /// <param name="typeName">The string name of the type to search for.</param>
    /// <returns>An Enumerator for matching types. List will be empty if bad params are supplied.</returns>
    public IEnumerable<Type> GetTypesByName(string typeName);

    /// <summary>
    /// Allows iteration over all types (including interfaces) in all loaded assemblies managed by the AsmMgr.
    /// Warning: High usage may result in performance issues.
    /// </summary>
    /// <returns>An Enumerator for iteration.</returns>
    public IEnumerable<Type> GetAllTypesInLoadedAssemblies();

    /// <summary>
    /// Returns a list of all loaded ACLs.
    /// WARNING: References to these ACLs outside the AssemblyManager should be kept in a WeakReference in order
    /// to avoid causing issues with unloading/disposal. 
    /// </summary>
    /// <returns></returns>
    public IEnumerable<AssemblyManager.LoadedACL> GetAllLoadedACLs();

    #endregion

    #region InternalAPI
    /*** Notes: Internal API uses the 'public' modifier because of the common and recommended use of publicized APIs
     * by third-party add-ins.
     */
    
    /// <summary>
    /// [Unsafe] Warning: only for use in nested threading functions. Requires care to manage access.
    /// Does not make any guarantees about the state of the ACL after the list has been returned.
    /// </summary>
    /// <returns></returns>
    public ImmutableList<AssemblyManager.LoadedACL> UnsafeGetAllLoadedACLs();
    
    /// <summary>
    /// Used by content package and plugin management to stop unloading of a given ACL until all plugins have gracefully closed.
    /// </summary>
    public event System.Func<AssemblyManager.LoadedACL, bool> IsReadyToUnloadACL;

    /// <summary>
    /// Compiles an assembly from supplied references and syntax trees into the specified AssemblyContextLoader.
    /// A new ACL will be created if the Guid supplied is Guid.Empty.
    /// </summary>
    /// <param name="compiledAssemblyName"></param>
    /// <param name="syntaxTree"></param>
    /// <param name="externalMetadataReferences"></param>
    /// <param name="compilationOptions"></param>
    /// <param name="friendlyName">A non-unique name for later reference. Optional, set to null if unused.</param>
    /// <param name="id">The guid of the assembly </param>
    /// <param name="externFileAssemblyRefs"></param>
    /// <returns></returns>
    public AssemblyLoadingSuccessState LoadAssemblyFromMemory([NotNull] string compiledAssemblyName,
        [NotNull] IEnumerable<SyntaxTree> syntaxTree,
        IEnumerable<MetadataReference> externalMetadataReferences,
        [NotNull] CSharpCompilationOptions compilationOptions,
        string friendlyName,
        ref Guid id,
        IEnumerable<Assembly> externFileAssemblyRefs = null);

    /// <summary>
    /// Switches the ACL with the given Guid to Template Mode, which disables assembly name resolution for any assemblies loaded in it.
    /// These ACLs are intended to be used to host Assemblies for information only and not for code execution.
    /// WARNING: This process is irreversible.
    /// </summary>
    /// <param name="guid">Guid of the ACL.</param>
    /// <returns>Whether an ACL was found with the given ID.</returns>
    public bool SetACLToTemplateMode(Guid guid);


    /// <summary>
    /// Tries to load all assemblies at the supplied file paths list into the ACl with the given Guid.
    /// If the supplied Guid is Empty, then a new ACl will be created and the Guid will be assigned to it.
    /// </summary>
    /// <param name="filePaths">List of assemblies to try and load.</param>
    /// <param name="friendlyName">A non-unique name for later reference. Optional.</param>
    /// <param name="id">Guid of the ACL or Empty if none specified. Guid of ACL will be assigned to this var.</param>
    /// <returns>Operation success messages.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public AssemblyLoadingSuccessState LoadAssembliesFromLocations([NotNull] IEnumerable<string> filePaths,
        string friendlyName, ref Guid id);


    /// <summary>
    /// Tries to begin the disposal process of ACLs.
    /// </summary>
    /// <returns>Returns whether the unloading process could be initiated.</returns>
    public bool TryBeginDispose();


    /// <summary>
    /// Returns whether unloading is completed and updates the styate of the unloading cache.
    /// </summary>
    /// <returns></returns>
    public bool FinalizeDispose();

    /// <summary>
    /// Tries to retrieve the LoadedACL with the given ID or null if none is found.
    /// WARNING: External references to this ACL with long lifespans should be kept in a WeakReference
    /// to avoid causing unloading/disposal issues.
    /// </summary>
    /// <param name="id">GUID of the ACL.</param>
    /// <param name="acl">The found ACL or null if none was found.</param>
    /// <returns>Whether an ACL was found.</returns>
    public bool TryGetACL(Guid id, out AssemblyManager.LoadedACL acl);

    #endregion
}
