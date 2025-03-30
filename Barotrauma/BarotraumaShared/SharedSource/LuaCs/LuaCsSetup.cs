using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Processing;
using Barotrauma.Networking;
using FluentResults;
using ImpromptuInterface;

namespace Barotrauma
{
    internal delegate void LuaCsMessageLogger(string message);
    internal delegate void LuaCsErrorHandler(Exception ex, LuaCsMessageOrigin origin);
    internal delegate void LuaCsExceptionHandler(Exception ex, LuaCsMessageOrigin origin);

    partial class LuaCsSetup : IDisposable, IEventScreenSelected, IEventAllPackageListChanged, IEventEnabledPackageListChanged, IEventReloadAllPackages
    {
        public LuaCsSetup()
        {
            // == startup
            _servicesProvider = new ServicesProvider();
            RegisterServices();
            ValidateLuaCsContent();
            SubscribeToLuaCsEvents();
            
            return;
            // == end
            
            // == sub processes
            void RegisterServices()
            {
                _servicesProvider.RegisterServiceType<IPackageListRetrievalService, PackageListRetrievalService>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IPackageInfoLookupService, ContentPackageInfoLookup>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<ILoggerService, LoggerService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<PerformanceCounterService, PerformanceCounterService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<IStorageService, StorageService>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IEventService, EventService>(ServiceLifetime.Singleton);
#if CLIENT
                _servicesProvider.RegisterServiceType<IStylesService, StylesService>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IStylesManagementService, StylesManagementService>(ServiceLifetime.Singleton);
#endif
                _servicesProvider.RegisterServiceType<IPackageManagementService, PackageManagementService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<IPluginManagementService, PluginManagementService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<ILuaScriptManagementService, LuaScriptManagementService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<LuaGame, LuaGame>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<ILocalizationService, LocalizationService>(ServiceLifetime.Singleton);
                
                // TODO: IConfigService
                // TODO: INetworkingService
                // TODO: [Resource Converter/Parser Services]
                
                // IResourceInfo wrappers and mutators.
                _servicesProvider.RegisterServiceType<IProcessorService<IReadOnlyList<IAssemblyResourceInfo>, IAssembliesResourcesInfo>, ResourceInfoArrayPacker>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IProcessorService<IReadOnlyList<IConfigResourceInfo>, IConfigsResourcesInfo>, ResourceInfoArrayPacker>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IProcessorService<IReadOnlyList<IConfigProfileResourceInfo>, IConfigProfilesResourcesInfo>, ResourceInfoArrayPacker>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IProcessorService<IReadOnlyList<ILocalizationResourceInfo>, ILocalizationsResourcesInfo>, ResourceInfoArrayPacker>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IProcessorService<IReadOnlyList<ILuaScriptResourceInfo>, ILuaScriptsResourcesInfo>, ResourceInfoArrayPacker>(ServiceLifetime.Transient);
                
                _servicesProvider.RegisterServiceType<IConverterService<ContentPackage, IModConfigInfo>, ModConfigService>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IConverterServiceAsync<ContentPackage, IModConfigInfo>, ModConfigService>(ServiceLifetime.Transient);
                _servicesProvider.RegisterServiceType<IConverterServiceAsync<ILocalizationResourceInfo, ImmutableArray<ILocalizationInfo>>, ResourceInfoLoaders>(ServiceLifetime.Transient);
                
                
                
                _servicesProvider.Compile();
            }

            // Validates LuaCs assets in /Content are valid and ready to use.
            void ValidateLuaCsContent()
            {
                // check if /Content/Lua/ModConfig.xml exists
                // if not, try to copy it from the Workshop Mod (ie. installation mode)
                // if that fails, throw an error and exit.
            }
        }
        
        void SubscribeToLuaCsEvents()
        {
            EventService.Subscribe<IEventScreenSelected>(this); // game state hook in
            EventService.Subscribe<IEventAllPackageListChanged>(this); 
            EventService.Subscribe<IEventEnabledPackageListChanged>(this); 
            EventService.Subscribe<IEventReloadAllPackages>(this);
        }
        
        #region CONST_DEF
        
#if SERVER
        public const bool IsServer = true;
        public const bool IsClient = false;
#else
        public const bool IsServer = false;
        public const bool IsClient = true;
#endif

        #endregion
        
        #region Services_ConfigVars

        /*
         * === Singleton Services
         */
        
        private readonly IServicesProvider _servicesProvider;
        
        public PerformanceCounterService PerformanceCounter => _servicesProvider.TryGetService<PerformanceCounterService>(out var svc)
            ? svc : throw new NullReferenceException("Performance counter service not found!");
        public ILoggerService Logger => _servicesProvider.TryGetService<ILoggerService>(out var svc) 
            ? svc : throw new NullReferenceException("Logger service not found!");
        public IConfigService ConfigService => _servicesProvider.TryGetService<IConfigService>(out var svc) 
            ? svc : throw new NullReferenceException("Config Manager service not found!");
        public IPackageManagementService PackageManagementService => _servicesProvider.TryGetService<IPackageManagementService>(out var svc) 
            ? svc : throw new NullReferenceException("Package Manager service not found!");
        public IPluginManagementService PluginManagementService => _servicesProvider.TryGetService<IPluginManagementService>(out var svc) 
            ? svc : throw new NullReferenceException("Plugin Manager service not found!");
        public ILuaScriptManagementService LuaScriptManagementService => _servicesProvider.TryGetService<ILuaScriptManagementService>(out var svc) 
            ? svc : throw new NullReferenceException("Lua Script Manager service not found!");
        public ILocalizationService LocalizationService => _servicesProvider.TryGetService<ILocalizationService>(out var svc) 
            ? svc : throw new NullReferenceException("Localization Manager service not found!");
        public INetworkingService NetworkingService => _servicesProvider.TryGetService<INetworkingService>(out var svc) 
            ? svc : throw new NullReferenceException("Networking Manager service not found!");
        public IEventService EventService => _servicesProvider.TryGetService<IEventService>(out var svc) 
            ? svc : throw new NullReferenceException("Networking Manager service not found!");
        public LuaGame Game => _servicesProvider.TryGetService<LuaGame>(out var svc)
            ? svc : throw new NullReferenceException("LuaGame service not found!");

        /*
         * === Config Vars 
         */
        
        /// <summary>
        /// Whether C# plugin code is enabled.
        /// </summary>
        public IConfigEntry<bool> IsCsEnabled { get; private set; }
        
        /// <summary>
        /// Whether mods marked as 'forced' or 'always load' should only be loaded if they're in the enabled mods list.
        /// </summary>
        public IConfigEntry<bool> TreatForcedModsAsNormal { get; private set; }
        
        /// <summary>
        /// Whether the lua script runner from Workshop package should be used over the in-built version.
        /// </summary>
        public IConfigEntry<bool> PreferToUseWorkshopLuaSetup { get; private set; }
        
        /// <summary>
        /// Whether the popup error GUI should be hidden/suppressed.
        /// </summary>
        public IConfigEntry<bool> DisableErrorGUIOverlay { get; private set; }
        
        /// <summary>
        /// Whether usernames are anonymized or show in logs. 
        /// </summary>
        public IConfigEntry<bool> HideUserNamesInLogs { get; private set; }
        
        /// <summary>
        /// The SteamId of the Workshop LuaCs CPackage in use, if available.
        /// </summary>
        public IConfigEntry<ulong> LuaForBarotraumaSteamId { get; private set; }
        
        /// <summary>
        /// The SteamId of the Workshop LuaCs CsForBarotrauma add-on, if available.
        /// </summary>
        public IConfigEntry<ulong> CsForBarotraumaSteamId { get; private set; }
        
        /// <summary>
        /// Whether to (re)load all package assets when a lobby starts/code session begins.
        /// Intended for development use, or when packages are expected to change outside of External Updates (ie. Steam Workshop). 
        /// </summary>
        public IConfigEntry<bool> ReloadPackagesOnLobbyStart { get; private set; }
        
        /// <summary>
        /// TODO: @evilfactory@users.noreply.github.com
        /// </summary>
        public IConfigEntry<bool> RestrictMessageSize { get; private set; }

        /**
         * == Ops Vars
         */
        private RunState _runState;
        /// <summary>
        /// The current run state of all services managed by LuaCs. 
        /// </summary>
        public RunState CurrentRunState => _runState;

        private bool CPacksParsed => CurrentRunState >= RunState.Parsed;
        private bool IsStaticAssetsLoaded => CurrentRunState >= RunState.Configuration;
        private bool IsCodeRunning => CurrentRunState >= RunState.Running;

        private readonly ConcurrentQueue<ContentPackage> _toLoad = new();
        private readonly ConcurrentQueue<ContentPackage> _toUnload = new();
        
        #endregion

        #region LegacyRedirects

        public ILuaCsHook Hook => this.EventService;        

        #endregion

        public static bool IsRunningInsideWorkshop
        {
            get
            {
#if SERVER
                return Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) != Directory.GetCurrentDirectory();
#else
                return false; // unnecessary but just keeps things clear that this is NOT for client stuff
#endif
            }
        }

        private partial bool ShouldRunCs();
        

        // TODO: Rework
        [Obsolete("Use IPluginManagementService::GetTypesByName()")]
        public static Type GetType(string typeName, bool throwOnError = false, bool ignoreCase = false)
        {
            throw new NotImplementedException();
            //return AssemblyManager.GetTypesByName(typeName).FirstOrDefault((Type)null);
        }

        public static ContentPackage GetPackage(ContentPackageId id, bool fallbackToAll = true, bool useBackup = false)
        {
            foreach (ContentPackage package in ContentPackageManager.EnabledPackages.All)
            {
                if (package.UgcId.ValueEquals(id))
                {
                    return package;
                }
            }

            if (fallbackToAll)
            {
                foreach (ContentPackage package in ContentPackageManager.LocalPackages)
                {
                    if (package.UgcId.ValueEquals(id))
                    {
                        return package;
                    }
                }

                foreach (ContentPackage package in ContentPackageManager.AllPackages)
                {
                    if (package.UgcId.ValueEquals(id))
                    {
                        return package;
                    }
                }
            }

            if (useBackup && ContentPackageManager.EnabledPackages.BackupPackages.Regular != null)
            {
                foreach (ContentPackage package in ContentPackageManager.EnabledPackages.BackupPackages.Regular.Value)
                {
                    if (package.UgcId.ValueEquals(id))
                    {
                        return package;
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            try
            {
                SetRunState(RunState.Unloaded);
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }

            try
            {
                DisposeLuaCsConfig();
                
                PluginManagementService.Dispose();
                LuaScriptManagementService.Dispose();
#if CLIENT
                StylesManagementService.Dispose();
#endif
                ConfigService.Dispose();
                LocalizationService.Dispose();
                PackageManagementService.Dispose();
                // TODO: Add all missing services.
                //NetworkingService.Dispose();
                EventService.Dispose();
                
                _servicesProvider.DisposeAndReset();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Handles changes in game states tracked by screen changes.
        /// </summary>
        /// <param name="screen">The new game screen.</param>
        public partial void OnScreenSelected(Screen screen);

        public void OnAllPackageListChanged(IEnumerable<CorePackage> corePackages, IEnumerable<RegularPackage> regularPackages)
        {
            UpdateLoadedPackagesList();
        }

        public void OnEnabledPackageListChanged(CorePackage corePackage, IEnumerable<RegularPackage> regularPackages)
        { 
            UpdateLoadedPackagesList();
        }
        
        public void OnReloadAllPackages()
        {
            if (CurrentRunState <= RunState.Unloaded)
                return;
            var state = CurrentRunState;
            SetRunState(RunState.Unloaded);
            SetRunState(CurrentRunState);
        }

        public void ForceRunState(RunState newState)
        {
            if (CurrentRunState == newState)
                return;
            SetRunState(newState);
        }

        private void UpdateLoadedPackagesList()
        {
            var newPackSet = ContentPackageManager.AllPackages
                .Union(ContentPackageManager.EnabledPackages.All)
                .ToHashSet();
            var currPackSet = PackageManagementService.GetAllLoadedPackages().ToHashSet();
            var toAdd = newPackSet.Except(currPackSet);
            var toRemove = currPackSet.Except(newPackSet);
            foreach (var package in toAdd)
                _toLoad.Enqueue(package);
            foreach (var package in toRemove)
                _toUnload.Enqueue(package);
            
            
            ProcessPackagesListDifferences();
        }

        void ProcessPackagesListDifferences()
        {
            if (IsCodeRunning)
                return;

            // no reason to do anything if we're fully unloaded.
            if (!CPacksParsed)
            {
                _toLoad.Clear();
                _toUnload.Clear();
            }
            
            while (_toUnload.TryDequeue(out var cp))
            {
                LuaScriptManagementService.DisposePackageResources(cp);
                ConfigService.DisposeConfigsProfiles(cp);
                ConfigService.DisposeConfigs(cp);
#if CLIENT
                StylesManagementService.DisposeStylesForPackage(cp);
#endif
                LocalizationService.DisposePackage(cp);
                PackageManagementService.DisposePackageInfos(cp);
            }
            
            var ls = new List<ContentPackage>();
                
            while (_toLoad.TryDequeue(out var cp))
            {
                if (PackageManagementService.LoadPackageInfosAsync(cp).GetAwaiter().GetResult() is
                    { IsFailed: true } failure)
                {
                    Logger.LogError($"Failed to load package infos for {cp.Name}");
                    Logger.LogResults(failure);
                    continue;
                }
                    
                ls.Add(cp);
            }

            if (ls.Any())
            {
                LoadStaticAssetsAsync(ls).GetAwaiter().GetResult();
            }
        }
        
        void SetRunState(RunState runState)
        {
            if (CurrentRunState == runState)
                return;
            if (runState > CurrentRunState)
            {
                if (CurrentRunState < RunState.Parsed)
                    LoadCurrentContentPackageInfos();
                
                if (runState <= CurrentRunState)
                    return;

                if (CurrentRunState < RunState.Configuration)
                    LoadStaticAssets();

                if (runState <= CurrentRunState)
                    return;

                if (CurrentRunState < RunState.Running)
                    RunScripts();
            }
            else if (runState < CurrentRunState)
            {
                if (CurrentRunState >= RunState.Running)
                {
                    StopScripts();
                    ProcessPackagesListDifferences();
                    _runState = RunState.Configuration;
                }
                
                if (runState >= CurrentRunState)
                    return;

                if (CurrentRunState == RunState.Configuration)
                {
                    UnloadStaticAssets();
                    _runState = RunState.Parsed;
                }
                
                if (runState >= CurrentRunState)
                    return;

                if (CurrentRunState == RunState.Parsed)
                {
                    UnloadContentPackageInfos();
                    _runState = RunState.Unloaded;
                }
                
                // we should be unloaded completely now | RunState.Unloaded
            }
        }

        void LoadCurrentContentPackageInfos()
        {
            if (CurrentRunState >= RunState.Parsed)
                return;
            
            // load core
            var result1 = PackageManagementService.LoadPackageInfosAsync(ContentPackageManager.VanillaCorePackage)
                .GetAwaiter().GetResult();
            if (result1.IsFailed)
            {
                Logger.LogError($"Unable to load LuaCs CorePackage resources! Running in degraded mode.");
                Logger.LogResults(result1);
            }
            
            // load regular
            var list = ContentPackageManager.RegularPackages
                .Union(ContentPackageManager.EnabledPackages.All)
                .ToImmutableList();
            
            LoadContentPackagesInfos(list);
            
            if (CurrentRunState < RunState.Parsed)
                _runState = RunState.Parsed;
        }

        void LoadContentPackagesInfos(IReadOnlyList<ContentPackage> packages)
        {
            var result2 = PackageManagementService.LoadPackagesInfosAsync(packages)
                .GetAwaiter().GetResult();
            
            foreach (var entry in result2)
            {
                if (entry.Item2.IsSuccess)
                    Logger.LogMessage($"Successfully parsed package: {entry.Item1.Name}");
                else if (entry.Item2.IsFailed)
                    Logger.LogResults(entry.Item2);
            }
        }

        void LoadStaticAssets()
        {
            if (CurrentRunState < RunState.Parsed)
            {
                throw new InvalidOperationException($"{nameof(LoadStaticAssets)} cannot load assets in the '{CurrentRunState}' state.");
            }
            
            if (CurrentRunState >= RunState.Configuration)
                return;

            while (_toUnload.TryDequeue(out var cp))
                PackageManagementService.DisposePackageInfos(cp);
            
            LoadStaticAssetsAsync(PackageManagementService.GetAllLoadedPackages()).GetAwaiter().GetResult();
            LoadLuaCsConfig();
            
            if (CurrentRunState < RunState.Configuration)
                _runState = RunState.Configuration;
        }

        void LoadLuaCsConfig()
        {
            IsCsEnabled = ConfigService.GetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "IsCsEnabled") 
                          ?? throw new NullReferenceException($"{nameof(IsCsEnabled)} cannot be loaded.");
            TreatForcedModsAsNormal = ConfigService.GetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "TreatForcedModsAsNormal") 
                                      ?? throw new NullReferenceException($"{nameof(TreatForcedModsAsNormal)} cannot be loaded.");
            DisableErrorGUIOverlay = ConfigService.GetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "DisableErrorGUIOverlay") 
                                     ?? throw new NullReferenceException($"{nameof(DisableErrorGUIOverlay)} cannot be loaded.");
            HideUserNamesInLogs = ConfigService.GetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "HideUserNamesInLogs") 
                                  ?? throw new NullReferenceException($"{nameof(HideUserNamesInLogs)} cannot be loaded.");
            LuaForBarotraumaSteamId = ConfigService.GetConfig<IConfigEntry<ulong>>(ContentPackageManager.VanillaCorePackage, "LuaForBarotraumaSteamId") 
                                      ?? throw new NullReferenceException($"{nameof(LuaForBarotraumaSteamId)} cannot be loaded.");
            CsForBarotraumaSteamId = ConfigService.GetConfig<IConfigEntry<ulong>>(ContentPackageManager.VanillaCorePackage, "CsForBarotraumaSteamId") 
                                     ?? throw new NullReferenceException($"{nameof(CsForBarotraumaSteamId)} cannot be loaded.");
            RestrictMessageSize = ConfigService.GetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "RestrictMessageSize") 
                                  ?? throw new NullReferenceException($"{nameof(RestrictMessageSize)} cannot be loaded.");
            ReloadPackagesOnLobbyStart = ConfigService.GetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "ReloadPackagesOnLobbyStart") 
                                         ?? throw new NullReferenceException($"{nameof(ReloadPackagesOnLobbyStart)} cannot be loaded.");
            
        }

        void DisposeLuaCsConfig()
        {
            IsCsEnabled = null;
            TreatForcedModsAsNormal = null;
            DisableErrorGUIOverlay = null;
            HideUserNamesInLogs = null;
            LuaForBarotraumaSteamId = null;
            CsForBarotraumaSteamId = null;
            RestrictMessageSize = null;
            ReloadPackagesOnLobbyStart = null;
        }

        async Task LoadStaticAssetsAsync(IReadOnlyList<ContentPackage> packages)
        {
            var locRes = ImmutableArray<ILocalizationResourceInfo>.Empty;
            var cfgRes = ImmutableArray<IConfigResourceInfo>.Empty;
            var cfpRes = ImmutableArray<IConfigProfileResourceInfo>.Empty;
            var luaRes = ImmutableArray<ILuaScriptResourceInfo>.Empty;

#if CLIENT
            var styleRes = ImmutableArray<IStylesResourceInfo>.Empty;
#endif
            
            var tasksBuilder = ImmutableArray.CreateBuilder<Task>();
                
            //---- get resource infos
            tasksBuilder.AddRange(new Func<Task>(async () =>
                {
                    var res = await PackageManagementService.GetLocalizationsInfosAsync(packages);
                    if (res.IsSuccess)
                        locRes = res.Value.Localizations;
                    if (res.Errors.Any())
                        ThreadPool.QueueUserWorkItem(state => Logger.LogResults((FluentResults.Result)state),
                            res.ToResult());
                })(),
                new Func<Task>(async () =>
                {
                    var res = await PackageManagementService.GetConfigsInfosAsync(packages);
                    if (res.IsSuccess)
                        cfgRes = res.Value.Configs;
                    if (res.Errors.Any())
                        ThreadPool.QueueUserWorkItem(state => Logger.LogResults((FluentResults.Result)state),
                            res.ToResult());
                })(),
                new Func<Task>(async () =>
                {
                    var res = await PackageManagementService.GetConfigProfilesInfosAsync(packages);
                    if (res.IsSuccess)
                        cfpRes = res.Value.ConfigProfiles;
                    if (res.Errors.Any())
                        ThreadPool.QueueUserWorkItem(state => Logger.LogResults((FluentResults.Result)state),
                            res.ToResult());
                })(),
                new Func<Task>(async () =>
                {
                    var res = await PackageManagementService.GetLuaScriptsInfosAsync(packages);
                    if (res.IsSuccess)
                        luaRes = res.Value.LuaScripts;
                    if (res.Errors.Any())
                        ThreadPool.QueueUserWorkItem(state => Logger.LogResults((FluentResults.Result)state),
                            res.ToResult());
                })());

#if CLIENT
            tasksBuilder.Add(new Func<Task>(async () =>
            {
                var res = await PackageManagementService.GetStylesInfosAsync(packages);
                if (res.IsSuccess)
                    styleRes = res.Value.Styles;
                if (res.Errors.Any())
                    ThreadPool.QueueUserWorkItem(state => Logger.LogResults((FluentResults.Result)state),
                        res.ToResult());
            })());
#endif
            await Task.WhenAll(tasksBuilder.MoveToImmutable());
            tasksBuilder.Clear();
      
            //---- load resources
            tasksBuilder.AddRange(new Func<Task>(async () =>
                {
                    var res = await ConfigService.LoadConfigsAsync(cfgRes);
                    if (res.Errors.Any())
                        Logger.LogResults(res);
                    res = await ConfigService.LoadConfigsProfilesAsync(cfpRes);
                    if (res.Errors.Any())
                        Logger.LogResults(res);
                })(),
                new Func<Task>(async () =>
                {
                    var res = await LuaScriptManagementService.LoadScriptResourcesAsync(luaRes);
                    if (res.Errors.Any())
                        Logger.LogResults(res);
                })());

#if CLIENT
            tasksBuilder.Add(new Func<Task>(async () =>
            {
                var res = await StylesManagementService.LoadStylesAsync(styleRes);
                if (res.Errors.Any())
                    Logger.LogResults(res);
            })());
#endif
            
            // load localizations first
            if (!locRes.IsDefaultOrEmpty)
            {
                var res = await LocalizationService.LoadLocalizations(locRes);
                if (res.Errors.Any())
                    Logger.LogResults(res);
            }

            await Task.WhenAll(tasksBuilder.MoveToImmutable());
        }
        
        void RunScripts()
        {   
            if (!IsStaticAssetsLoaded)
            {
                throw new InvalidOperationException($"{nameof(RunScripts)} cannot load assets in the '{CurrentRunState}' state.");
            }
            
            if (CurrentRunState >= RunState.Running)
                return;

            if (ShouldRunCs())
            {
                var asmRes =
                    PackageManagementService.GetAssembliesInfos(PackageManagementService
                        .GetAllLoadedPackages()
                        .Where(ContentPackageManager.EnabledPackages.All.Contains)
                        .ToList());
                if (asmRes.IsFailed)
                {
                    Logger.LogError($"{nameof(RunScripts)}: Errors will retrieving assembly resources, cannot load scripts!");
                    Logger.LogResults(asmRes.ToResult());
                    return;
                }
                var res = PluginManagementService.LoadAssemblyResources(asmRes.Value.Assemblies);
                if (res.IsFailed)
                {
                    Logger.LogError($"{nameof(RunScripts)}: Failed to initialize scripts!");
                    Logger.LogResults(res.ToResult());
                }
                else
                {
                    if (res.Errors.Any())
                        Logger.LogResults(res.ToResult());
                    if (PluginManagementService.GetImplementingTypes<IAssemblyPlugin>() is {IsSuccess: true} types)
                    {
                        var typeInst = PluginManagementService.ActivateTypeInstances<IAssemblyPlugin>(types.Value, true, true);
                        foreach (var loadRes in typeInst)
                        {
                            if (loadRes is { IsSuccess: true, Value: { Item2: { } pluginInstance } })
                            {
                                EventService.Subscribe<IEventPluginPreInitialize>(pluginInstance);
                                EventService.Subscribe<IEventPluginInitialize>(pluginInstance);
                                EventService.Subscribe<IEventPluginLoadCompleted>(pluginInstance);
                            }
                            else
                            {
                                Logger.LogResults(loadRes.ToResult());
                            }
                        }

                        EventService.PublishEvent<IEventPluginPreInitialize>(sub => sub.PreInitPatching());
                        EventService.PublishEvent<IEventPluginInitialize>(sub => sub.Initialize());
                        EventService.PublishEvent<IEventPluginLoadCompleted>(sub => sub.OnLoadCompleted());
                    }  
                }
            }

            //lua
            var luaRes = PackageManagementService.GetLuaScriptsInfos(PackageManagementService
                .GetAllLoadedPackages()
                .Where(ContentPackageManager.EnabledPackages.All.Contains)
                .ToList());
            if (luaRes.IsFailed)
            {
                Logger.LogError($"{nameof(RunScripts)}: Failed to get enabled lua script resources!");
                Logger.LogResults(luaRes.ToResult());
                return;
            }
            
            if (luaRes.Errors.Any())
                Logger.LogResults(luaRes.ToResult());
            
            
            LuaScriptManagementService.ExecuteLoadedScripts(luaRes.Value.LuaScripts);
            
            if (CurrentRunState < RunState.Running)
                _runState = RunState.Running;
        }

        void UnloadContentPackageInfos()
        {
            if (IsStaticAssetsLoaded)
            {
                throw new InvalidOperationException($"{nameof(UnloadStaticAssets)}: Cannot unload static assets when the current run state is {CurrentRunState}.");
            }

            PackageManagementService.Reset();
            _toUnload.Clear();
        }

        void UnloadStaticAssets()
        {
            if (IsCodeRunning)
            {
                throw new InvalidOperationException($"{nameof(UnloadStaticAssets)}: Cannot unload static assets when the current run state is {CurrentRunState}.");
            }

            PluginManagementService.Reset();    
            LuaScriptManagementService.Reset();
            ConfigService.Reset();
#if CLIENT
            StylesManagementService.Reset();
#endif
            LocalizationService.Reset();

            if (CurrentRunState >= RunState.Configuration)
            {
                _runState = RunState.Parsed;
            }
        }

        void StopScripts()
        {
            EventService.ClearAllSubscribers();
            LuaScriptManagementService.UnloadActiveScripts();
            PluginManagementService.UnloadAllAssemblyResources();
            SubscribeToLuaCsEvents();

            if (IsCodeRunning)
            {
                _runState = RunState.Configuration;
            }
        }

        
    }

    /// <summary>
    /// Specifies the current run state of the LuaCs Modding System.
    /// <b>[Important]Enum State values ordering must be in the form of (lower state) === (higher state)</b>
    /// </summary>
    public enum RunState : byte
    {
        Unloaded = 0,   // No asset data loaded.
        Parsed,         // CPacks' ResourceInfos are parsed.
        Configuration,  // localization and configuration assets loaded.
        Running         // all assets loaded, code running.
    }
}
