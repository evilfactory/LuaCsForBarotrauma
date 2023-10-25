using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Barotrauma.Steam;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MonoMod.Utils;
// ReSharper disable InconsistentNaming

namespace Barotrauma;

public sealed class CsPackageManager : IDisposable
{
    #region PRIVATE_FUNCDATA

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
#if CLIENT
            .AppendLine("[assembly: IgnoresAccessChecksTo(\"Barotrauma\")]")
#elif SERVER
            .AppendLine("[assembly: IgnoresAccessChecksTo(\"DedicatedServer\")]")
#endif
            .ToString(),
        ScriptParseOptions);

    private const string SCRIPT_FILE_REGEX = "*.cs";
    private const string ASSEMBLY_FILE_REGEX = "*.dll";

    private readonly float _assemblyUnloadTimeoutSeconds = 6f;
    private Guid _publicizedAssemblyLoader;
    private readonly List<ContentPackage> _currentPackagesByLoadOrder = new();
    private readonly Dictionary<ContentPackage, ImmutableList<ContentPackage>> _packagesDependencies = new();
    private readonly Dictionary<ContentPackage, Guid> _loadedCompiledPackageAssemblies = new();
    private readonly Dictionary<Guid, ContentPackage> _reverseLookupGuidList = new();
    private readonly Dictionary<Guid, HashSet<IAssemblyPlugin>> _loadedPlugins = new ();
    private readonly Dictionary<Guid, ImmutableHashSet<Type>> _pluginTypes = new(); // where Type : IAssemblyPlugin
    private readonly Dictionary<ContentPackage, RunConfig> _packageRunConfigs = new();
    private readonly Dictionary<Guid, ImmutableList<Type>> _luaRegisteredTypes = new();
    private readonly AssemblyManager _assemblyManager;
    private readonly LuaCsSetup _luaCsSetup;
    private DateTime _assemblyUnloadStartTime;


    #endregion

    #region PUBLIC_API

    #region LUA_EXTENSIONS

    /// <summary>
    /// Searches for all types in all loaded assemblies from content packages who's names contain the name string and registers them with the Lua Interpreter. 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="caseSensitive"></param>
    /// <returns></returns>
    public bool LuaTryRegisterPackageTypes(string name, bool caseSensitive = false)
    {
        if (!AssembliesLoaded)
            return false;
        var matchingPacks = _loadedCompiledPackageAssemblies
            .Where(kvp => kvp.Key.Name.ToLowerInvariant().Contains(name.ToLowerInvariant()))
            .Select(kvp => kvp.Value)
            .ToImmutableList();
        if (!matchingPacks.Any())
            return false;
        var types = matchingPacks
            .Where(guid => !_luaRegisteredTypes.ContainsKey(guid))
            .Select(guid => new KeyValuePair<Guid, ImmutableList<Type>>(
                guid, 
                _assemblyManager.TryGetSubTypesFromACL(guid, out var types) 
                    ? types.ToImmutableList()
                    : ImmutableList<Type>.Empty))
            .ToImmutableList();
        if (!types.Any())
            return false;
        foreach (var kvp in types)
        {
            _luaRegisteredTypes[kvp.Key] = kvp.Value;
            foreach (Type type in kvp.Value)
            {
                MoonSharp.Interpreter.UserData.RegisterType(type);
            }
        }

        return true;
    }

    #endregion
    
    /// <summary>
    /// Whether or not assemblies have been loaded.
    /// </summary>
    public bool AssembliesLoaded { get; private set; }
    
    
    /// <summary>
    /// Whether or not loaded plugins had their preloader run.
    /// </summary>
    public bool PluginsPreInit { get; private set; }
    
    /// <summary>
    /// Whether or not plugins' types have been instantiated.
    /// </summary>
    public bool PluginsInitialized { get; private set; }

    /// <summary>
    /// Whether or not plugins are fully loaded.
    /// </summary>
    public bool PluginsLoaded { get; private set; }

    public IEnumerable<ContentPackage> GetCurrentPackagesByLoadOrder() => _currentPackagesByLoadOrder;

    /// <summary>
    /// Tries to find the content package that a given plugin belongs to. 
    /// </summary>
    /// <param name="package">Package if found, null otherwise.</param>
    /// <typeparam name="T">The IAssemblyPlugin type to find.</typeparam>
    /// <returns></returns>
    public bool TryGetPackageForPlugin<T>(out ContentPackage package) where T : IAssemblyPlugin
    {
        package = null;
        
        var t = typeof(T);
        var guid = _pluginTypes
            .Where(kvp => kvp.Value.Contains(t))
            .Select(kvp => kvp.Key)
            .FirstOrDefault(Guid.Empty);

        if (guid.Equals(Guid.Empty) || !_reverseLookupGuidList.ContainsKey(guid) || _reverseLookupGuidList[guid] is null)
            return false;
        package = _reverseLookupGuidList[guid];
        return true;
    }


    /// <summary>
    /// Tries to get the loaded plugins for a given package.
    /// </summary>
    /// <param name="package">Package to find.</param>
    /// <param name="loadedPlugins">The collection of loaded plugins.</param>
    /// <returns></returns>
    public bool TryGetLoadedPluginsForPackage(ContentPackage package, out IEnumerable<IAssemblyPlugin> loadedPlugins)
    {
        loadedPlugins = null;
        if (package is null || !_loadedCompiledPackageAssemblies.ContainsKey(package))
            return false;
        var guid = _loadedCompiledPackageAssemblies[package];
        if (guid.Equals(Guid.Empty) || !_loadedPlugins.ContainsKey(guid))
            return false;
        loadedPlugins = _loadedPlugins[guid];
        return true;
    }

    /// <summary>
    /// Called when clean up is being performed. Use when relying on or making use of references from this manager.
    /// </summary>
    public event Action OnDispose; 

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Dispose()
    {
        // send events for cleanup
        try
        {
            OnDispose?.Invoke();
        }
        catch (Exception e)
        {
            ModUtils.Logging.PrintError($"Error while executing Dispose event: {e.Message}");
        }
        
        // cleanup events
        if (OnDispose is not null)
        {
            foreach (Delegate del in OnDispose.GetInvocationList())
            {
                OnDispose -= (del as System.Action);
            }
        }
        
        // cleanup plugins and assemblies
        ReflectionUtils.ResetCache();
        UnloadPlugins();
        // try cleaning up the assemblies
        _pluginTypes.Clear();   // remove assembly references
        _loadedPlugins.Clear();
        _publicizedAssemblyLoader = Guid.Empty;
        _packagesDependencies.Clear();
        _loadedCompiledPackageAssemblies.Clear();
        _reverseLookupGuidList.Clear();
        _packageRunConfigs.Clear();
        _currentPackagesByLoadOrder.Clear();

        // lua cleanup
        foreach (var kvp in _luaRegisteredTypes)
        {
            foreach (Type type in kvp.Value)
            {
                MoonSharp.Interpreter.UserData.UnregisterType(type);
            }
        }
        _luaRegisteredTypes.Clear();

        _assemblyUnloadStartTime = DateTime.Now;
        _publicizedAssemblyLoader = Guid.Empty;
        
        // we can't wait forever or app dies but we can try to be graceful
        while (!_assemblyManager.TryBeginDispose())
        {
            Thread.Sleep(20);   // give the assembly context unloader time to run (async)
            if (_assemblyUnloadStartTime.AddSeconds(_assemblyUnloadTimeoutSeconds) > DateTime.Now)
            {
                break;
            }
        }
        
        _assemblyUnloadStartTime = DateTime.Now;
        Thread.Sleep(100);  // give the garbage collector time to finalize the disposed assemblies.
        while (!_assemblyManager.FinalizeDispose())
        {
            Thread.Sleep(100);  // give the garbage collector time to finalize the disposed assemblies.
            if (_assemblyUnloadStartTime.AddSeconds(_assemblyUnloadTimeoutSeconds) > DateTime.Now)
            {
                break;
            }
        }
        
        _assemblyManager.OnAssemblyLoaded -= AssemblyManagerOnAssemblyLoaded;
        _assemblyManager.OnAssemblyUnloading -= AssemblyManagerOnAssemblyUnloading;

        AssembliesLoaded = false;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Begins the loading process of scanning packages for scripts and binary assemblies, compiling and executing them.
    /// </summary>
    /// <returns></returns>
    public AssemblyLoadingSuccessState LoadAssemblyPackages()
    {
        if (AssembliesLoaded)
        {
            return AssemblyLoadingSuccessState.AlreadyLoaded;
        }
        
        _assemblyManager.OnAssemblyLoaded += AssemblyManagerOnAssemblyLoaded;
        _assemblyManager.OnAssemblyUnloading += AssemblyManagerOnAssemblyUnloading;

        // log error if some ACLs are still unloading (some assembly is still in use)
        if (_assemblyManager.IsCurrentlyUnloading)
        {
            ModUtils.Logging.PrintWarning($"WARNING: Some mods from a previous session (lobby) are still loaded! This may result in undefined behaviour!\nIf you notice any odd behaviour that only occurs after multiple lobbies, please restart your game.");
            foreach (var wkref in _assemblyManager.StillUnloadingACLs)
            {
                ModUtils.Logging.PrintWarning($"The below ACL is still unloading:");
                if (wkref.TryGetTarget(out var tgt))
                {
                    ModUtils.Logging.PrintWarning($"ACL Name: {tgt.Name}");
                    foreach (Assembly assembly in tgt.Assemblies)
                    {
                        ModUtils.Logging.PrintWarning($"-- Assembly: {assembly.GetName()}");
                    }
                }
            }
        }
        
        // load publicized assemblies
        var publicizedDir = Path.Combine(Environment.CurrentDirectory, "Publicized");
        
        // if using workshop lua setup is checked, try to use the publicized assemblies in the content package there instead.
        if (_luaCsSetup.Config.PreferToUseWorkshopLuaSetup)
        {
            var pck = LuaCsSetup.GetPackage(LuaCsSetup.LuaForBarotraumaId);
            if (pck is not null)
            {
                publicizedDir = Path.Combine(pck.Dir, "Binary", "Publicized");
            }
        }
        
        ImmutableList<Assembly> publicizedAssemblies = ImmutableList<Assembly>.Empty;
        ImmutableList<string> list;
        
        try
        {
            // search for assemblies
            list = Directory.GetFiles(publicizedDir, "*.dll")
#if CLIENT
                .Where(s => !s.ToLowerInvariant().EndsWith("dedicatedserver.dll"))
#elif SERVER
                .Where(s => !s.ToLowerInvariant().EndsWith("barotrauma.dll"))
#endif
                .ToImmutableList();

            if (list.Count < 1)
                throw new DirectoryNotFoundException("No publicized assemblies found.");
        }
        // no directory found, use the other one
        catch (DirectoryNotFoundException)
        {
            if (_luaCsSetup.Config.PreferToUseWorkshopLuaSetup)
            {
                ModUtils.Logging.PrintError($"Unable to find <LuaCsPackage>/Binary/Publicized/ . Using Game folder instead.");
                publicizedDir = Path.Combine(Environment.CurrentDirectory, "Publicized");
            }
            else
            {
                ModUtils.Logging.PrintError($"Unable to find <GameFolder>/Publicized/ . Using LuaCsPackage folder instead.");
                var pck = LuaCsSetup.GetPackage(LuaCsSetup.LuaForBarotraumaId);
                if (pck is not null)
                {
                    publicizedDir = Path.Combine(pck.Dir, "Binary", "Publicized");
                }
            }
            
            // search for assemblies
            list = Directory.GetFiles(publicizedDir, "*.dll")
#if CLIENT
                .Where(s => !s.ToLowerInvariant().EndsWith("dedicatedserver.dll"))
#elif SERVER
                .Where(s => !s.ToLowerInvariant().EndsWith("barotrauma.dll"))
#endif
                .ToImmutableList();
        }

        // try load them into an acl
        var loadState = _assemblyManager.LoadAssembliesFromLocations(list, "luacs_publicized_assemblies", ref _publicizedAssemblyLoader);

        // loaded
        if (loadState is AssemblyLoadingSuccessState.Success)
        {
            if (_assemblyManager.TryGetACL(_publicizedAssemblyLoader, out var acl))
            {
                publicizedAssemblies = acl.Acl.Assemblies.ToImmutableList();
                _assemblyManager.SetACLToTemplateMode(_publicizedAssemblyLoader);
            }
        }


        // get packages
        IEnumerable<ContentPackage> packages = BuildPackagesList();
        
        // check and load config
        _packageRunConfigs.AddRange(packages
            .Select(p => new KeyValuePair<ContentPackage, RunConfig>(p, GetRunConfigForPackage(p)))
            .ToDictionary(p => p.Key, p=> p.Value));
        
        // filter not to be loaded
        var cpToRunA = _packageRunConfigs
            .Where(kvp => ShouldRunPackage(kvp.Key, kvp.Value))
            .Select(kvp => kvp.Key)
            .ToHashSet();
        
        //-- filter and remove duplicate mods, prioritize /LocalMods/
        HashSet<string> cpNames = new();
        HashSet<string> duplicateNames = new();

        // search
        foreach (ContentPackage package in cpToRunA)
        {
            if (cpNames.Contains(package.Name))
            {
                if (!duplicateNames.Contains(package.Name))
                {
                    duplicateNames.Add(package.Name);
                }
            }
            else
            {
                cpNames.Add(package.Name);
            }        
        }

        // remove
        foreach (string name in duplicateNames)
        {
            var duplCpList = cpToRunA
                .Where(p => p.Name.Equals(name))
                .ToHashSet();
            
            if (duplCpList.Count < 2)   // one or less found
                continue;

            ContentPackage toKeep = null;
            foreach (ContentPackage package in duplCpList)
            {
                if (package.Dir.Contains("LocalMods"))
                {
                    toKeep = package;
                    break;
                }
            }

            toKeep ??= duplCpList.First();

            duplCpList.Remove(toKeep);  // remove all but this one
            cpToRunA.RemoveWhere(p => duplCpList.Contains(p));
        }

        var cpToRun = cpToRunA.ToImmutableList();

        // build dependencies map
        bool reliableMap = TryBuildDependenciesMap(cpToRun, out var packDeps);
        if (!reliableMap)
        {
            ModUtils.Logging.PrintMessage($"{nameof(CsPackageManager)}: Unable to create reliable dependencies map.");
        }
        
        _packagesDependencies.AddRange(packDeps.ToDictionary(
            kvp => kvp.Key, 
            kvp => kvp.Value.ToImmutableList())
        );

        List<ContentPackage> packagesToLoadInOrder = new();

        // build load order
        if (reliableMap && OrderAndFilterPackagesByDependencies(
                _packagesDependencies,
                out var readyToLoad,
                out var cannotLoadPackages))
        {
            packagesToLoadInOrder.AddRange(readyToLoad);
            if (cannotLoadPackages is not null)
            {
                ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Unable to load the following mods due to dependency errors:");
                foreach (var pair in cannotLoadPackages)
                {
                    ModUtils.Logging.PrintError($"Package: {pair.Key.Name} | Reason: {pair.Value}");
                }
            }
        }
        else
        {
            // use unsorted list on failure and send error message.
            packagesToLoadInOrder.AddRange(_packagesDependencies.Select( p=> p.Key));
            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Unable to create a reliable load order. Defaulting to unordered loading!");
        }
        
        // get assemblies and scripts' filepaths from packages
        var toLoad = packagesToLoadInOrder
            .Select(cp => new KeyValuePair<ContentPackage, LoadableData>(
                cp,
                new LoadableData(
                    TryScanPackagesForAssemblies(cp, out var list1) ? list1 : null,
                    TryScanPackageForScripts(cp, out var list2) ? list2 : null,
                    GetRunConfigForPackage(cp))))
            .ToImmutableDictionary();
        
        HashSet<ContentPackage> badPackages = new();
        foreach (var pair in toLoad)
        {
            // check if unloadable
            if (badPackages.Contains(pair.Key))
                continue;

            // try load binary assemblies
            var id = Guid.Empty;    // id for the ACL for this package defined by AssemblyManager.
            AssemblyLoadingSuccessState successState;
            if (pair.Value.AssembliesFilePaths is not null && pair.Value.AssembliesFilePaths.Any())
            {
                ModUtils.Logging.PrintMessage($"Loading assemblies for CPackage {pair.Key.Name}");
#if DEBUG
                foreach (string assembliesFilePath in pair.Value.AssembliesFilePaths)
                {
                    ModUtils.Logging.PrintMessage($"Found assemblies located at {Path.GetFullPath(ModUtils.IO.SanitizePath(assembliesFilePath))}");
                }
#endif
                
                successState = _assemblyManager.LoadAssembliesFromLocations(pair.Value.AssembliesFilePaths, pair.Key.Name, ref id);

                // error handling
                if (successState is not AssemblyLoadingSuccessState.Success)
                {
                    ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Unable to load the binary assemblies for package {pair.Key.Name}. Error: {successState.ToString()}");
                    UpdatePackagesToDisable(ref badPackages, pair.Key, _packagesDependencies);
                    continue;
                }
            }
            
            // try compile scripts to assemblies
            if (pair.Value.ScriptsFilePaths is not null && pair.Value.ScriptsFilePaths.Any())
            {
                ModUtils.Logging.PrintMessage($"Loading scripts for CPackage {pair.Key.Name}");
                List<SyntaxTree> syntaxTrees = new();
            
                syntaxTrees.Add(GetPackageScriptImports());
                bool abortPackage = false;
                // load scripts data from files
                foreach (string scriptPath in pair.Value.ScriptsFilePaths)
                {
                    var state = ModUtils.IO.GetOrCreateFileText(scriptPath, out string fileText, null, false);
                    // could not load file data
                    if (state is not ModUtils.IO.IOActionResultState.Success)
                    {
                        ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Unable to load the script files for package {pair.Key.Name}. Error: {state.ToString()}");
                        UpdatePackagesToDisable(ref badPackages, pair.Key, _packagesDependencies);
                        abortPackage = true;
                        break;
                    }

                    try
                    {
                        CancellationToken token = new();
                        syntaxTrees.Add(SyntaxFactory.ParseSyntaxTree(fileText, ScriptParseOptions, scriptPath, Encoding.Default, token));
                        // cancel if parsing failed
                        if (token.IsCancellationRequested)
                        {
                            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Unable to load the script files for package {pair.Key.Name}. Error: Syntax Parse Error.");
                            UpdatePackagesToDisable(ref badPackages, pair.Key, _packagesDependencies);
                            abortPackage = true;
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        // unknown error
                        ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Unable to load the script files for package {pair.Key.Name}. Error: {e.Message}");
                        UpdatePackagesToDisable(ref badPackages, pair.Key, _packagesDependencies);
                        abortPackage = true;
                        break;
                    }
                
                }
                
                if (abortPackage)
                    continue;
            
                // try compile
                successState = _assemblyManager.LoadAssemblyFromMemory(
                    pair.Value.config.UseInternalAssemblyName ? "CompiledAssembly" : pair.Key.Name.Replace(" ",""), 
                    syntaxTrees, 
                    null, 
                    CompilationOptions, 
                     pair.Key.Name, 
                    ref id, 
                    pair.Value.config.UseNonPublicizedAssemblies ? null : publicizedAssemblies);

                if (successState is not AssemblyLoadingSuccessState.Success)
                {
                    ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Unable to compile script assembly for package {pair.Key.Name}. Error: {successState.ToString()}");
                    UpdatePackagesToDisable(ref badPackages, pair.Key, _packagesDependencies);
                    continue;
                }
            }

            // something was loaded, add to index
            if (id != Guid.Empty)
            {
                ModUtils.Logging.PrintMessage($"Assemblies from CPackage {pair.Key.Name} loaded with Guid {id}.");
                _loadedCompiledPackageAssemblies.Add(pair.Key, id);
                _reverseLookupGuidList.Add(id, pair.Key);
            }
        }

        // update loaded packages to exclude bad packages
        _currentPackagesByLoadOrder.AddRange(toLoad
            .Where(p => !badPackages.Contains(p.Key))
            .Select(p => p.Key));

        // build list of plugins
        foreach (var pair in _loadedCompiledPackageAssemblies)
        {
            if (_assemblyManager.TryGetSubTypesFromACL<IAssemblyPlugin>(pair.Value, out var types))
            {
                _pluginTypes[pair.Value] = types.ToImmutableHashSet();
                foreach (var type in _pluginTypes[pair.Value])
                {
                    ModUtils.Logging.PrintMessage($"Loading type: {type.Name}");
                }
            }
        }

        this.AssembliesLoaded = true;
        return AssemblyLoadingSuccessState.Success;
        

        bool ShouldRunPackage(ContentPackage package, RunConfig config)
        {
            return (!_luaCsSetup.Config.TreatForcedModsAsNormal && config.IsForced())
                   || (ContentPackageManager.EnabledPackages.All.Contains(package) && config.IsForcedOrStandard());
        }

        void UpdatePackagesToDisable(ref HashSet<ContentPackage> set, 
            ContentPackage newDisabledPackage, 
            IEnumerable<KeyValuePair<ContentPackage, ImmutableList<ContentPackage>>> dependenciesMap)
        {
            set.Add(newDisabledPackage);
            foreach (var package in dependenciesMap)
            {
                if (package.Value.Contains(newDisabledPackage))
                    set.Add(newDisabledPackage);
            }
        }
    }
    
    /// <summary>
    /// Executes instantiated plugins' Initialize() and OnLoadCompleted() methods. 
    /// </summary>
    public void RunPluginsInit()
    {
        if (!AssembliesLoaded)
        {
            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Attempted to call plugins' Initialize() without any loaded assemblies!");
            return;
        }
        
        if (!PluginsInitialized)
        {
            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Attempted to call plugins' Initialize() without type instantiation!");
            return;
        }
     
        if (PluginsLoaded)
            return;
        
        foreach (var contentPlugins in _loadedPlugins)
        {
            // init
            foreach (var plugin in contentPlugins.Value)
            {
                TryRun(() => plugin.Initialize(), $"{nameof(IAssemblyPlugin.Initialize)}", $"CP: {_reverseLookupGuidList[contentPlugins.Key].Name} Plugin: {plugin.GetType().Name}");
            }
        }
        
        foreach (var contentPlugins in _loadedPlugins)
        {
            // load complete
            foreach (var plugin in contentPlugins.Value)
            {
                TryRun(() => plugin.OnLoadCompleted(), $"{nameof(IAssemblyPlugin.OnLoadCompleted)}", $"CP: {_reverseLookupGuidList[contentPlugins.Key].Name} Plugin: {plugin.GetType().Name}");
            }
        }

        PluginsLoaded = true;
    }

    /// <summary>
    /// Executes instantiated plugins' PreInitPatching() method. 
    /// </summary>
    public void RunPluginsPreInit()
    {
        if (!AssembliesLoaded)
        {
            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Attempted to call plugins' PreInitPatching() without any loaded assemblies!");
            return;
        }

        if (!PluginsInitialized)
        {
            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Attempted to call plugins' PreInitPatching() without type initialization!");
            return;
        }
        
        if (PluginsPreInit)
        {
            return;
        }
        
        foreach (var contentPlugins in _loadedPlugins)
        {
            // init
            foreach (var plugin in contentPlugins.Value)
            {
                TryRun(() => plugin.PreInitPatching(), $"{nameof(IAssemblyPlugin.PreInitPatching)}", $"CP: {_reverseLookupGuidList[contentPlugins.Key].Name} Plugin: {plugin.GetType().Name}");
            }
        }

        PluginsPreInit = true;
    }

    /// <summary>
    /// Initializes plugin types that are registered.
    /// </summary>
    /// <param name="force"></param>
    public void InstantiatePlugins(bool force = false)
    {
        if (!AssembliesLoaded)
        {
            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Attempted to instantiate plugins without any loaded assemblies!");
            return;
        }
        
        if (PluginsInitialized)
        {
            if (force)
                UnloadPlugins();
            else
            {
                ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Attempted to load plugins when they were already loaded!");
                return;
            }
        }

        foreach (var pair in _pluginTypes)
        {
            // instantiate
            foreach (Type type in pair.Value)
            {
                if (!_loadedPlugins.ContainsKey(pair.Key))
                    _loadedPlugins.Add(pair.Key, new());
                else if (_loadedPlugins[pair.Key] is null)
                    _loadedPlugins[pair.Key] = new();
                IAssemblyPlugin plugin = null;
                try
                {
                   plugin = (IAssemblyPlugin)Activator.CreateInstance(type);
                   _loadedPlugins[pair.Key].Add(plugin);
                }
                catch (Exception e)
                {
                    ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Error while instantiating plugin of type {type}. Now disposing...");
                    ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Details: {e.Message} | {e.InnerException}");

                    if (plugin is not null)
                    {
                        // ReSharper disable once AccessToModifiedClosure
                        TryRun(() => plugin?.Dispose(), nameof(IAssemblyPlugin.Dispose), type.FullName ?? type.Name);
                        plugin = null;
                    }
                }
            }
        }

        PluginsInitialized = true;
    }

    /// <summary>
     /// Unloads all plugins by calling Dispose() on them. Note: This does not remove their external references nor
     /// unregister their types. 
     /// </summary>
    public void UnloadPlugins()
    {
        foreach (var contentPlugins in _loadedPlugins)
        {
            foreach (var plugin in contentPlugins.Value)
            {
                TryRun(() => plugin.Dispose(), $"{nameof(IAssemblyPlugin.Dispose)}", $"CP: {_reverseLookupGuidList[contentPlugins.Key].Name} Plugin: {plugin.GetType().Name}");
            }
            contentPlugins.Value.Clear();
        }
        
        _loadedPlugins.Clear();

        PluginsInitialized = false;
        PluginsPreInit = false;
        PluginsLoaded = false;
    }
    
    
    /// <summary>
    /// Gets the RunConfig.xml for the given package located at [cp_root]/CSharp/RunConfig.xml.
    /// Generates a default config if one is not found. 
    /// </summary>
    /// <param name="package">The package to search for.</param>
    /// <param name="config">RunConfig data.</param>
    /// <returns>True if a config is loaded, false if one was created.</returns>
    public static bool GetOrCreateRunConfig(ContentPackage package, out RunConfig config)
    {
        var path = System.IO.Path.Combine(Path.GetFullPath(package.Dir), "CSharp", "RunConfig.xml");
        if (!File.Exists(path))
        {
            config = new RunConfig(true).Sanitize();
            return false;
        }
        return ModUtils.IO.LoadOrCreateTypeXml(out config, path, () => new RunConfig(true).Sanitize(), false);
    }
    
    #endregion

    #region INTERNALS

    private void TryRun(Action action, string messageMethodName, string messageTypeName)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception e)
        {
            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Error while running {messageMethodName}() on plugin of type {messageTypeName}");
            ModUtils.Logging.PrintError($"{nameof(CsPackageManager)}: Details: {e.Message} | {e.InnerException}");
        }
    }
    
    private void AssemblyManagerOnAssemblyUnloading(Assembly assembly)
    {
        ReflectionUtils.RemoveAssemblyFromCache(assembly);
    }

    private void AssemblyManagerOnAssemblyLoaded(Assembly assembly)
    {
        //ReflectionUtils.AddNonAbstractAssemblyTypes(assembly);
        // As ReflectionUtils.GetDerivedNonAbstract is only used for Prefabs & Barotrauma-specific implementing types,
        // we can safely not register System/Core assemblies.
        if (assembly.FullName is not null && assembly.FullName.StartsWith("System."))
            return;
        ReflectionUtils.AddNonAbstractAssemblyTypes(assembly, true);
    }
    
    internal CsPackageManager([NotNull] AssemblyManager assemblyManager, [NotNull] LuaCsSetup luaCsSetup)
    {
        this._assemblyManager = assemblyManager;
        this._luaCsSetup = luaCsSetup;
    }

    ~CsPackageManager()
    {
        this.Dispose();
    }

    private static bool TryScanPackageForScripts(ContentPackage package, out ImmutableList<string> scriptFilePaths)
    {
        string pathShared = Path.Combine(ModUtils.IO.GetContentPackageDir(package), "CSharp", "Shared");
        string pathArch = Path.Combine(ModUtils.IO.GetContentPackageDir(package), "CSharp", ARCHITECTURE_TARGET);

        List<string> files = new();

        if (Directory.Exists(pathShared))
            files.AddRange(Directory.GetFiles(pathShared, SCRIPT_FILE_REGEX, SearchOption.AllDirectories));
        if (Directory.Exists(pathArch))
            files.AddRange(Directory.GetFiles(pathArch, SCRIPT_FILE_REGEX, SearchOption.AllDirectories));

        if (files.Count > 0)
        {
            scriptFilePaths = files.ToImmutableList();
            return true;
        }
        scriptFilePaths = ImmutableList<string>.Empty;
        return false;
    }

    private static bool TryScanPackagesForAssemblies(ContentPackage package, out ImmutableList<string> assemblyFilePaths)
    {
        string path = Path.Combine(ModUtils.IO.GetContentPackageDir(package), "bin", ARCHITECTURE_TARGET, PLATFORM_TARGET);

        if (!Directory.Exists(path))
        {
            assemblyFilePaths = ImmutableList<string>.Empty;
            return false;
        }

        assemblyFilePaths = System.IO.Directory.GetFiles(path, ASSEMBLY_FILE_REGEX, SearchOption.AllDirectories)
            .ToImmutableList();
        return assemblyFilePaths.Count > 0;
    }

    private static RunConfig GetRunConfigForPackage(ContentPackage package)
    {
        if (!GetOrCreateRunConfig(package, out var config))
            config.AutoGenerated = true;
        return config;
    }
    
    private IEnumerable<ContentPackage> BuildPackagesList()
    {
        // get unique list of content packages. 
        // Note: there is an old issue where the AllPackages group
        // would sometimes not contain packages downloaded from the host, so we union enabled.
        return ContentPackageManager.AllPackages.Union(ContentPackageManager.EnabledPackages.All).Where(pack => !pack.Name.ToLowerInvariant().Equals("vanilla"));
    }


    private static SyntaxTree GetPackageScriptImports() => BaseAssemblyImports;

    
    /// <summary>
    /// Builds a list of ContentPackage dependencies for each of the packages in the list. Note: All dependencies must be included in the provided list of packages.
    /// </summary>
    /// <param name="packages">List of packages to check</param>
    /// <param name="dependenciesMap">Dependencies by package</param>
    /// <returns>True if all dependencies were found.</returns>
    private static bool TryBuildDependenciesMap(ImmutableList<ContentPackage> packages, out Dictionary<ContentPackage, List<ContentPackage>> dependenciesMap)
    {
        bool reliableMap = true;    // remains true if all deps were found.
        dependenciesMap = new();
        foreach (var package in packages)
        {
            dependenciesMap.Add(package, new());
            if (GetOrCreateRunConfig(package, out var config))
            {
                if (config.Dependencies is null || !config.Dependencies.Any())
                    continue;
            
                foreach (RunConfig.Dependency dependency in config.Dependencies)
                {
                    ContentPackage dep = packages.FirstOrDefault(p => 
                        (dependency.SteamWorkshopId != 0 && p.TryExtractSteamWorkshopId(out var steamWorkshopId) 
                                                         && steamWorkshopId.Value == dependency.SteamWorkshopId) 
                        || (!dependency.PackageName.IsNullOrWhiteSpace() && p.Name.ToLowerInvariant().Contains(dependency.PackageName.ToLowerInvariant())), null);

                    if (dep is not null)
                    {
                        dependenciesMap[package].Add(dep);
                    }
                    else
                    {
                        ModUtils.Logging.PrintError($"Warning! The ContentPackage {package.Name} lists a dependency of (STEAMID: {dependency.SteamWorkshopId}, PackageName: {dependency.PackageName}) but it could not be found in the to-be-loaded CSharp packages list!");
                        reliableMap = false;
                    }
                }    
            }
            else
            {
                ModUtils.Logging.PrintMessage($"Warning! Could not retrieve RunConfig for ContentPackage {package.Name}!");
            }
        }
        
        return reliableMap;
    }
    
    /// <summary>
    /// Given a table of packages and dependent packages, will sort them by dependency loading order along with packages
    /// that cannot be loaded due to errors or failing the predicate checks.
    /// </summary>
    /// <param name="packages">A dictionary/map with key as the package and the elements as it's dependencies.</param>
    /// <param name="readyToLoad">List of packages that are ready to load and in the correct order.</param>
    /// <param name="cannotLoadPackages">Packages with errors or cyclic dependencies. Element is error message. Null if empty.</param>
    /// <param name="packageChecksPredicate">Optional: Allows for a custom checks to be performed on each package.
    /// Returns a bool indicating if the package is ready to load.</param>
    /// <returns>Whether or not the process produces a usable list.</returns>
    private static bool OrderAndFilterPackagesByDependencies(
        Dictionary<ContentPackage, ImmutableList<ContentPackage>> packages,
        out IEnumerable<ContentPackage> readyToLoad,
        out IEnumerable<KeyValuePair<ContentPackage, string>> cannotLoadPackages,
        Func<ContentPackage, bool> packageChecksPredicate = null)
    {
        HashSet<ContentPackage> completedPackages = new();
        List<ContentPackage> readyPackages = new();
        Dictionary<ContentPackage, string> unableToLoad = new();
        HashSet<ContentPackage> currentNodeChain = new();

        readyToLoad = readyPackages;

        try
        {
            foreach (var toProcessPack in packages)
            {
                ProcessPackage(toProcessPack.Key, toProcessPack.Value);
            }

            PackageProcRet ProcessPackage(ContentPackage packageToProcess, IEnumerable<ContentPackage> dependencies)
            {
                //cyclic handling
                if (unableToLoad.ContainsKey(packageToProcess))
                {
                    return PackageProcRet.BadPackage;
                }

                // already processed
                if (completedPackages.Contains(packageToProcess))
                {
                    return PackageProcRet.AlreadyCompleted;
                }

                // cyclic check
                if (currentNodeChain.Contains(packageToProcess))
                {
                    StringBuilder sb = new();
                    sb.AppendLine("Error: Cyclic Dependency. ")
                        .Append(
                            "The following ContentPackages rely on eachother in a way that makes it impossible to know which to load first! ")
                        .Append(
                            "Note: the package listed twice shows where the cycle starts/ends and is not necessarily the problematic package.");
                    int i = 0;
                    foreach (var package in currentNodeChain)
                    {
                        i++;
                        sb.AppendLine($"{i}. {package.Name}");
                    }

                    sb.AppendLine($"{i}. {packageToProcess.Name}");
                    unableToLoad.Add(packageToProcess, sb.ToString());
                    completedPackages.Add(packageToProcess);
                    return PackageProcRet.BadPackage;
                }

                if (packageChecksPredicate is not null && !packageChecksPredicate.Invoke(packageToProcess))
                {
                    unableToLoad.Add(packageToProcess, $"Unable to load package {packageToProcess.Name} due to failing checks.");
                    completedPackages.Add(packageToProcess);
                    return PackageProcRet.BadPackage;
                }

                currentNodeChain.Add(packageToProcess);

                foreach (ContentPackage dependency in dependencies)
                {
                    // The mod lists a dependent that was not found during the discovery phase.
                    if (!packages.ContainsKey(dependency))
                    {
                        // search to see if it's enabled
                        if (!ContentPackageManager.EnabledPackages.All.Contains(dependency))
                        {
                            // present warning but allow loading anyways, better to let the user just disable the package if it's really an issue.
                            ModUtils.Logging.PrintError(
                                $"Warning: the ContentPackage of {packageToProcess.Name} requires the Dependency {dependency.Name} but this package wasn't found in the enabled mods list!");
                        }

                        continue;
                    }

                    var ret = ProcessPackage(dependency, packages[dependency]);

                    if (ret is PackageProcRet.BadPackage)
                    {
                        if (!unableToLoad.ContainsKey(packageToProcess))
                        {
                            unableToLoad.Add(packageToProcess, $"Error: Dependency failure. Failed to load {dependency.Name}");
                        }
                        currentNodeChain.Remove(packageToProcess);
                        if (!completedPackages.Contains(packageToProcess))
                        {
                            completedPackages.Add(packageToProcess);
                        }
                        return PackageProcRet.BadPackage;
                    }
                }
                
                currentNodeChain.Remove(packageToProcess);
                completedPackages.Add(packageToProcess);
                readyPackages.Add(packageToProcess); 
                return PackageProcRet.Completed;
            }
        }
        catch (Exception e)
        {
            ModUtils.Logging.PrintError($"Error while generating dependency loading order! Exception: {e.Message}");
#if DEBUG
            ModUtils.Logging.PrintError($"Stack Trace: {e.StackTrace}");
#endif
            cannotLoadPackages = unableToLoad.Any() ? unableToLoad : null;
            return false;
        }
        cannotLoadPackages = unableToLoad.Any() ? unableToLoad : null;
        return true;
    }

    private enum PackageProcRet : byte
    {
        AlreadyCompleted,
        Completed,
        BadPackage
    }

    private record LoadableData(ImmutableList<string> AssembliesFilePaths, ImmutableList<string> ScriptsFilePaths, RunConfig config);

    #endregion
}
