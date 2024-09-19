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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// ReSharper disable EventNeverSubscribedTo.Global
// ReSharper disable InconsistentNaming

namespace Barotrauma.LuaCs.Services;

/***
 * Note: This class was written to be thread-safe in order to allow parallelization in loading in the future if the need
 * becomes necessary as there is almost no serial performance overhead for adding threading protection. 
 */

/// <summary>
/// Provides functionality for the loading, unloading and management of plugins implementing IAssemblyPlugin.
/// All plugins are loaded into their own AssemblyLoadContext along with their dependencies.
/// </summary>
public class AssemblyManager : IAssemblyManagementService
{
    #region ExternalAPI

    public event Action<Assembly> OnAssemblyLoaded;
    public event Action<Assembly> OnAssemblyUnloading; 
    public event Action<string, Exception> OnException;
    public event Action<Guid> OnACLUnload; 
    public ImmutableList<WeakReference<MemoryFileAssemblyContextLoader>> StillUnloadingACLs
    {
        get
        {
            OpsLockUnloaded.EnterReadLock();
            try
            {
                return UnloadingACLs.ToImmutableList();
            }
            finally
            {
                OpsLockUnloaded.ExitReadLock();
            }
        }
    }
    public bool IsCurrentlyUnloading
    {
        get
        {
            OpsLockUnloaded.EnterReadLock();
            try
            {
                return UnloadingACLs.Any();
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                OpsLockUnloaded.ExitReadLock();
            }
        }
    }
    public IEnumerable<Type> GetSubTypesInLoadedAssemblies<T>(bool rebuildList)
    {
        Type targetType = typeof(T);
        string typeName = targetType.FullName ?? targetType.Name;

        // rebuild
        if (rebuildList)
            RebuildTypesList();
        
        // check cache
        if (_subTypesLookupCache.TryGetValue(typeName, out var subTypeList))
        {
            return subTypeList;
        }

        // build from scratch
        OpsLockLoaded.EnterReadLock();
        try
        {
            // build list
            var list1 = _defaultContextTypes
                .Where(kvp1 => targetType.IsAssignableFrom(kvp1.Value) && !kvp1.Value.IsInterface)
                .Concat(LoadedACLs
                    .SelectMany(kvp => kvp.Value.AssembliesTypes)
                    .Where(kvp2 => targetType.IsAssignableFrom(kvp2.Value) && !kvp2.Value.IsInterface))
                .Select(kvp3 => kvp3.Value)
                .ToImmutableList();

            // only add if we find something
            if (list1.Count > 0)
            {
                if (!_subTypesLookupCache.TryAdd(typeName, list1))
                {
                    ModUtils.Logging.PrintError(
                        $"{nameof(AssemblyManager)}: Unable to add subtypes to cache of type {typeName}!");
                }
            }
            else
            {
                ModUtils.Logging.PrintMessage(
                    $"{nameof(AssemblyManager)}: Warning: No types found during search for subtypes of {typeName}");
            }

            return list1;
        }
        catch (Exception e)
        {
            this.OnException?.Invoke($"{nameof(AssemblyManager)}::{nameof(GetSubTypesInLoadedAssemblies)}() | Error: {e.Message}", e);
            return ImmutableList<Type>.Empty;
        }
        finally
        {
            OpsLockLoaded.ExitReadLock();
        }
    }
    public bool TryGetSubTypesFromACL<T>(Guid id, out IEnumerable<Type> types)
    {
        Type targetType = typeof(T);

        if (TryGetACL(id, out var acl))
        {
            types = acl.AssembliesTypes
                .Where(kvp => targetType.IsAssignableFrom(kvp.Value) && !kvp.Value.IsInterface)
                .Select(kvp => kvp.Value);
            return true;
        }

        types = null;
        return false;
    }
    public bool TryGetSubTypesFromACL(Guid id, out IEnumerable<Type> types)
    {
        if (TryGetACL(id, out var acl))
        {
            types = acl.AssembliesTypes.Select(kvp => kvp.Value);
            return true;
        }

        types = null;
        return false;
    }
    public IEnumerable<Type> GetTypesByName(string typeName)
    {
        List<Type> types = new();
        if (typeName.IsNullOrWhiteSpace())
            return types;
        
        bool byRef = false;
        if (typeName.StartsWith("out ") || typeName.StartsWith("ref "))
        {
            typeName = typeName.Remove(0, 4);
            byRef = true;
        }
        
        
        TypesListHelper();
        if (types.Count > 0)
            return types;
        
        // we couldn't find it, rebuild and try one more time
        RebuildTypesList();
        TypesListHelper();

        if (types.Count > 0)
            return types;
        
        OpsLockLoaded.EnterReadLock();
        try
        {
            // fallback to Type.GetType
            Type t = Type.GetType(typeName, false, false);
            if (t is not null)
            {
                types.Add(byRef ? t.MakeByRefType() : t);
                return types;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = assembly.GetType(typeName, false, false);
                    if (t is not null)
                        types.Add(byRef ? t.MakeByRefType() : t);
                }
                catch (Exception e)
                {
                    this.OnException?.Invoke(
                        $"{nameof(AssemblyManager)}::{nameof(GetTypesByName)}() | Error: {e.Message}", e);
                }
            }
        }
        finally
        {
            OpsLockLoaded.ExitReadLock();
        }
        
        return types;

        void TypesListHelper()
        {
            if (_defaultContextTypes.TryGetValue(typeName, out var type1))
            {
                if (type1 is not null)
                    types.Add(byRef ? type1.MakeByRefType() : type1);
            }
        
            OpsLockLoaded.EnterReadLock();
            try
            {
                foreach (KeyValuePair<Guid,LoadedACL> loadedAcl in LoadedACLs)
                {
                    var at = loadedAcl.Value.AssembliesTypes;
                    if (at.TryGetValue(typeName, out var type2))
                    {
                        if (type2 is not null)
                            types.Add(byRef ? type2.MakeByRefType() : type2);
                    }
                }
            }
            finally
            {
                OpsLockLoaded.ExitReadLock();
            }
        }
    }
    public IEnumerable<Type> GetAllTypesInLoadedAssemblies()
    {
        OpsLockLoaded.EnterReadLock();
        try
        {
            return _defaultContextTypes
                .Select(kvp => kvp.Value)
                .Concat(LoadedACLs
                    .SelectMany(kvp => kvp.Value?.AssembliesTypes.Select(kv => kv.Value)))
                .ToImmutableList();
        }
        catch
        {
            return ImmutableList<Type>.Empty;
        }
        finally
        {
            OpsLockLoaded.ExitReadLock();
        }
    }
    public IEnumerable<LoadedACL> GetAllLoadedACLs()
    {
        OpsLockLoaded.EnterReadLock();
        try
        {
            if (!LoadedACLs.Any())
            {
                return ImmutableList<LoadedACL>.Empty;
            }

            return LoadedACLs.Select(kvp => kvp.Value).ToImmutableList();
        }
        catch
        {
            return ImmutableList<LoadedACL>.Empty;
        }
        finally
        {
            OpsLockLoaded.ExitReadLock();
        }
    }

    #endregion

    #region InternalAPI

    [MethodImpl(MethodImplOptions.Synchronized | MethodImplOptions.NoInlining)]
    ImmutableList<LoadedACL> IAssemblyManagementService.UnsafeGetAllLoadedACLs()
    {
        if (LoadedACLs.IsEmpty)
            return ImmutableList<LoadedACL>.Empty;
        return LoadedACLs.Select(kvp => kvp.Value).ToImmutableList();
    }
    public event System.Func<LoadedACL, bool> IsReadyToUnloadACL;
    public AssemblyLoadingSuccessState LoadAssemblyFromMemory([NotNull] string compiledAssemblyName,
        [NotNull] IEnumerable<SyntaxTree> syntaxTree,
        IEnumerable<MetadataReference> externalMetadataReferences,
        [NotNull] CSharpCompilationOptions compilationOptions,
        string friendlyName,
        ref Guid id,
        IEnumerable<Assembly> externFileAssemblyRefs = null)
    {
        // validation
        if (compiledAssemblyName.IsNullOrWhiteSpace())
            return AssemblyLoadingSuccessState.BadName;

        if (syntaxTree is null)
            return AssemblyLoadingSuccessState.InvalidAssembly;
        
        if (!GetOrCreateACL(id, friendlyName, out var acl))
            return AssemblyLoadingSuccessState.ACLLoadFailure;

        id = acl.Id;    // pass on true id returned
        
        // this acl is already hosting an in-memory assembly
        if (acl.Acl.CompiledAssembly is not null)
            return AssemblyLoadingSuccessState.AlreadyLoaded;

        // compile
        AssemblyLoadingSuccessState state;
        string messages;
        try
        {
            state = acl.Acl.CompileAndLoadScriptAssembly(compiledAssemblyName, syntaxTree, externalMetadataReferences,
                compilationOptions, out messages, externFileAssemblyRefs);
        }
        catch (Exception e)
        {
            ModUtils.Logging.PrintError($"{nameof(AssemblyManager)}::{nameof(LoadAssemblyFromMemory)}() | Failed to compile and load assemblies for [ {compiledAssemblyName} / {friendlyName} ]! Details: {e.Message} | {e.StackTrace}");
            return AssemblyLoadingSuccessState.InvalidAssembly;
        }

        // get types
        if (state is AssemblyLoadingSuccessState.Success)
        {
            _subTypesLookupCache.Clear();
            acl.RebuildTypesList();
            OnAssemblyLoaded?.Invoke(acl.Acl.CompiledAssembly);
        }
        else
        {
            ModUtils.Logging.PrintError($"Unable to compile assembly '{compiledAssemblyName}' due to errors: {messages}");
        }

        return state;
    }
    public bool SetACLToTemplateMode(Guid guid)
    {
        if (!TryGetACL(guid, out var acl))
            return false;
        acl.Acl.IsTemplateMode = true;
        return true;
    }
    public AssemblyLoadingSuccessState LoadAssembliesFromLocations([NotNull] IEnumerable<string> filePaths,
        string friendlyName, ref Guid id)
    {

        if (filePaths is null)
        {
            var exception = new ArgumentNullException(
                $"{nameof(AssemblyManager)}::{nameof(LoadAssembliesFromLocations)}() | file paths supplied is null!");
            this.OnException?.Invoke($"Error: {exception.Message}", exception);
            throw exception;
        }
        
        ImmutableList<string> assemblyFilePaths = filePaths.ToImmutableList();  // copy the list before loading

        if (!assemblyFilePaths.Any())
        {
            return AssemblyLoadingSuccessState.NoAssemblyFound;
        }
        
        if (GetOrCreateACL(id, friendlyName, out var loadedAcl))
        {
            var state = loadedAcl.Acl.LoadFromFiles(assemblyFilePaths);
            // if failure, we dispose of the acl
            if (state != AssemblyLoadingSuccessState.Success)
            {
                DisposeACL(loadedAcl.Id);
                ModUtils.Logging.PrintError($"ACL {friendlyName} failed, unloading...");
                return state;
            }
            // build types list
            _subTypesLookupCache.Clear();
            loadedAcl.RebuildTypesList();
            id = loadedAcl.Id;
            foreach (Assembly assembly in loadedAcl.Acl.Assemblies)
            {
                OnAssemblyLoaded?.Invoke(assembly);
            }
            return state;
        }

        return AssemblyLoadingSuccessState.ACLLoadFailure;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TryBeginDispose()
    {
        OpsLockLoaded.EnterWriteLock();
        OpsLockUnloaded.EnterWriteLock();
        try
        {
            _subTypesLookupCache.Clear();
            _defaultContextTypes = _defaultContextTypes.Clear();

            foreach (KeyValuePair<Guid, LoadedACL> loadedAcl in LoadedACLs)
            {
                if (loadedAcl.Value.Acl is not null)
                {
                    if (IsReadyToUnloadACL is not null)
                    {
                        foreach (Delegate del in IsReadyToUnloadACL.GetInvocationList())
                        {
                            if (del is System.Func<LoadedACL, bool> { } func)
                            {
                                if (!func.Invoke(loadedAcl.Value))
                                    return false; // Not ready, exit
                            }
                        }
                    }

                    foreach (Assembly assembly in loadedAcl.Value.Acl.Assemblies)
                    {
                        OnAssemblyUnloading?.Invoke(assembly);
                    }

                    UnloadingACLs.Add(new WeakReference<MemoryFileAssemblyContextLoader>(loadedAcl.Value.Acl, true));
                    loadedAcl.Value.ClearTypesList();
                    loadedAcl.Value.Acl.Unload();
                    loadedAcl.Value.ClearACLRef();
                    OnACLUnload?.Invoke(loadedAcl.Value.Id);
                }
            }

            LoadedACLs.Clear();
            return true;
        }
        catch(Exception e)
        {
            // should never happen
            this.OnException?.Invoke($"{nameof(TryBeginDispose)}() | Error: {e.Message}", e);
            return false;
        }
        finally
        {
            OpsLockUnloaded.ExitWriteLock();
            OpsLockLoaded.ExitWriteLock();
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool FinalizeDispose()
    {
        bool isUnloaded;
        OpsLockUnloaded.EnterUpgradeableReadLock();
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced); // force the gc to collect unloaded acls.
            List<WeakReference<MemoryFileAssemblyContextLoader>> toRemove = new();
            foreach (WeakReference<MemoryFileAssemblyContextLoader> weakReference in UnloadingACLs)
            {
                if (!weakReference.TryGetTarget(out _))
                {
                    toRemove.Add(weakReference);
                }
            }

            if (toRemove.Any())
            {
                OpsLockUnloaded.EnterWriteLock();
                try
                {
                    foreach (WeakReference<MemoryFileAssemblyContextLoader> reference in toRemove)
                    {
                        UnloadingACLs.Remove(reference);
                    }
                }
                finally
                {
                    OpsLockUnloaded.ExitWriteLock();
                }
            }
            isUnloaded = !UnloadingACLs.Any();
        }
        finally
        {
            OpsLockUnloaded.ExitUpgradeableReadLock();
        }

        return isUnloaded;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TryGetACL(Guid id, out LoadedACL acl)
    {
        acl = null;
        OpsLockLoaded.EnterReadLock();
        try
        {
            if (id.Equals(Guid.Empty) || !LoadedACLs.ContainsKey(id))
                return false;
            acl = LoadedACLs[id];
            return true;
        }
        finally
        {
            OpsLockLoaded.ExitReadLock();
        }
    }
    

    /// <summary>
    /// Gets or creates an AssemblyCtxLoader for the given ID. Creates if the ID is empty or no ACL can be found.
    /// [IMPORTANT] After calling this method, the id you use should be taken from the acl container (acl.Id). 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="friendlyName">A non-unique name for later reference. Optional.</param>
    /// <param name="acl"></param>
    /// <returns>Should only return false if an error occurs.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool GetOrCreateACL(Guid id, string friendlyName, out LoadedACL acl)
    {
        OpsLockLoaded.EnterUpgradeableReadLock();
        try
        {
            if (id.Equals(Guid.Empty) || !LoadedACLs.ContainsKey(id) || LoadedACLs[id] is null)
            {
                OpsLockLoaded.EnterWriteLock();
                try
                {
                    id = Guid.NewGuid();
                    acl = new LoadedACL(id, this, friendlyName);
                    LoadedACLs[id] = acl;
                    return true;
                }
                finally
                {
                    OpsLockLoaded.ExitWriteLock();
                }
            }
            else
            {
                acl = LoadedACLs[id];
                return true;
            }

        }
        catch(Exception e)
        {
            this.OnException?.Invoke($"{nameof(GetOrCreateACL)}Error: {e.Message}", e);
            acl = null;
            return false;
        }
        finally
        {
            OpsLockLoaded.ExitUpgradeableReadLock();
        }
    }
    

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool DisposeACL(Guid id)
    {
        OpsLockLoaded.EnterWriteLock();
        OpsLockUnloaded.EnterWriteLock();
        try
        {
            if (LoadedACLs.ContainsKey(id) && LoadedACLs[id] == null)
            {
                if (!LoadedACLs.TryRemove(id, out _))
                {
                    ModUtils.Logging.PrintWarning($"An ACL with the GUID {id.ToString()} was found as null. Unable to remove null ACL entry.");
                }
            }
            
            if (id.Equals(Guid.Empty) || !LoadedACLs.ContainsKey(id))
            {
                return false; // nothing to dispose of
            }

            var acl = LoadedACLs[id];

            foreach (Assembly assembly in acl.Acl.Assemblies)
            {
                OnAssemblyUnloading?.Invoke(assembly);
            }
            
            _subTypesLookupCache.Clear();
            UnloadingACLs.Add(new WeakReference<MemoryFileAssemblyContextLoader>(acl.Acl, true));
            acl.Acl.Unload();
            acl.ClearACLRef();
            OnACLUnload?.Invoke(acl.Id);

            return true;
        }
        catch (Exception e)
        {
            this.OnException?.Invoke($"{nameof(DisposeACL)}() | Error: {e.Message}", e);
            return false;
        }
        finally
        {
            OpsLockLoaded.ExitWriteLock();
            OpsLockUnloaded.ExitWriteLock();
        }
    }

    internal AssemblyManager()
    {
       RebuildTypesList();
    }

    /// <summary>
    /// Rebuilds the list of types in the default assembly load context.
    /// </summary>
    private void RebuildTypesList()
    {
        try
        {
            _defaultContextTypes = AssemblyLoadContext.Default.Assemblies
                .SelectMany(a => a.GetSafeTypes())
                .ToImmutableDictionary(t => t.FullName ?? t.Name, t => t);
            _subTypesLookupCache.Clear();
        }
        catch(ArgumentException ae)
        {
            this.OnException?.Invoke($"{nameof(RebuildTypesList)}() | Error: {ae.Message}", ae);
            try
            {
                // some types must've had duplicate type names, build the list while filtering
                Dictionary<string, Type> types = new();
                foreach (var type in AssemblyLoadContext.Default.Assemblies.SelectMany(a => a.GetSafeTypes()))
                {
                    try
                    {
                        types.TryAdd(type.FullName ?? type.Name, type);
                    }
                    catch
                    {
                        // ignore, null key exception
                    }
                }

                _defaultContextTypes = types.ToImmutableDictionary();
            }
            catch (Exception e)
            {
                this.OnException?.Invoke($"{nameof(RebuildTypesList)}() | Error: {e.Message}", e);
                ModUtils.Logging.PrintError($"{nameof(AssemblyManager)}: Unable to create list of default assembly types! Default AssemblyLoadContext types searching not available.");
#if DEBUG
                ModUtils.Logging.PrintError($"{nameof(AssemblyManager)}: Exception Details :{e.Message} | {e.InnerException}");
#endif
                _defaultContextTypes = ImmutableDictionary<string, Type>.Empty;
            }
        }
    }
    
    #endregion

    #region Data

    private readonly ConcurrentDictionary<string, ImmutableList<Type>> _subTypesLookupCache = new();
    private ImmutableDictionary<string, Type> _defaultContextTypes;
    private readonly ConcurrentDictionary<Guid, LoadedACL> LoadedACLs = new();
    private readonly List<WeakReference<MemoryFileAssemblyContextLoader>> UnloadingACLs= new();
    private readonly ReaderWriterLockSlim OpsLockLoaded = new ();
    private readonly ReaderWriterLockSlim OpsLockUnloaded = new ();

    #endregion

    #region TypeDefs
    

    public sealed class LoadedACL
    {
        public readonly Guid Id;
        private ImmutableDictionary<string, Type> _assembliesTypes = ImmutableDictionary<string, Type>.Empty;
        public MemoryFileAssemblyContextLoader Acl { get; private set; }

        internal LoadedACL(Guid id, AssemblyManager manager, string friendlyName)
        {
            this.Id = id;
            this.Acl = new(manager)
            {
                FriendlyName = friendlyName
            };
        }
        public ref readonly ImmutableDictionary<string, Type> AssembliesTypes => ref _assembliesTypes;

        /// <summary>
        /// Warning: For use by the Assembly Manager only! Do not call this method otherwise.
        /// </summary>
        internal void ClearACLRef()
        {
            Acl = null;
        }
        
        /// <summary>
        /// Rebuild the list of types from assemblies loaded in the AsmCtxLoader.
        /// </summary>
        internal void RebuildTypesList()
        {
            if (this.Acl is null)
            {
                ModUtils.Logging.PrintWarning($"{nameof(RebuildTypesList)}() | ACL with GUID {Id.ToString()} is null, cannot rebuild.");
                return;
            }
            
            ClearTypesList();
            try
            {
                _assembliesTypes = this.Acl.Assemblies
                    .SelectMany(a => a.GetSafeTypes())
                    .ToImmutableDictionary(t => t.FullName ?? t.Name, t => t);
            }
            catch(ArgumentException)
            {
                // some types must've had duplicate type names, build the list while filtering
                Dictionary<string, Type> types = new();
                foreach (var type in this.Acl.Assemblies.SelectMany(a => a.GetSafeTypes()))
                {
                    try
                    {
                        types.TryAdd(type.FullName ?? type.Name, type);
                    }
                    catch
                    {
                        // ignore, null key exception
                    }
                }

                _assembliesTypes = types.ToImmutableDictionary();
            }
        }

        internal void ClearTypesList()
        {
            _assembliesTypes = ImmutableDictionary<string, Type>.Empty;
        }
    }

    #endregion

    public void Dispose()
    {
        TryBeginDispose();
    }

    public void Reset()
    {
        TryBeginDispose();
    }
}

public static class AssemblyExtensions
{
    /// <summary>
    /// Gets all types in the given assembly. Handles invalid type scenarios.
    /// </summary>
    /// <param name="assembly">The assembly to scan</param>
    /// <returns>An enumerable collection of types.</returns>
    public static IEnumerable<Type> GetSafeTypes(this Assembly assembly)
    {
        // Based on https://github.com/Qkrisi/ktanemodkit/blob/master/Assets/Scripts/ReflectionHelper.cs#L53-L67

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException re)
        {
            try
            {
                return re.Types.Where(x => x != null)!;
            }
            catch (InvalidOperationException)   
            {
                return new List<Type>();
            }
        }
        catch (Exception)
        {
            return new List<Type>();
        }
    }
}
