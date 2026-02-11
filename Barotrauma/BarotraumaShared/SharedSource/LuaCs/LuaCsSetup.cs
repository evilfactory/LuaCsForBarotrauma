using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Compatibility;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using FluentResults;
using ImpromptuInterface;
using LightInject;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AssemblyLoader = Barotrauma.LuaCs.AssemblyLoader;

[assembly: InternalsVisibleTo("ImpromptuInterfaceDynamicAssembly")]
[assembly: InternalsVisibleTo("Dynamitey")]
namespace Barotrauma
{
    internal delegate void LuaCsMessageLogger(string message);
    internal delegate void LuaCsErrorHandler(Exception ex, LuaCsMessageOrigin origin);
    internal delegate void LuaCsExceptionHandler(Exception ex, LuaCsMessageOrigin origin);

    partial class LuaCsSetup : IDisposable, IEventScreenSelected, IEventEnabledPackageListChanged, 
        IEventReloadAllPackages
    {
        public LuaCsSetup()
        {
            // == startup
            _servicesProvider = SetupServicesProvider();
            _runStateMachine = SetupStateMachine();
            _servicesProvider.GetService<HarmonyEventPatchesService>();
            SubscribeToLuaCsEvents();
        }
        
        private void SubscribeToLuaCsEvents()
        {
            EventService.Subscribe<IEventScreenSelected>(this); // game state hook in
            EventService.Subscribe<IEventEnabledPackageListChanged>(this); 
            EventService.Subscribe<IEventReloadAllPackages>(this);
        }
        
        #region CONST_DEF
        
#if SERVER
        public const bool IsServer = true;
#else
        public const bool IsServer = false;
#endif
        public const bool IsClient = !IsServer;

        #endregion
        
        #region Services_CVars
        
        /*
         * === Singleton Services
         */
        
        private readonly IServicesProvider _servicesProvider;
        
        public PerformanceCounterService PerformanceCounter => _servicesProvider.GetService<PerformanceCounterService>();
        public ILoggerService Logger => _servicesProvider.GetService<ILoggerService>();
        public IConfigService ConfigService => _servicesProvider.GetService<IConfigService>();
        public IPackageManagementService PackageManagementService => _servicesProvider.GetService<IPackageManagementService>();
        public IPluginManagementService PluginManagementService => _servicesProvider.GetService<IPluginManagementService>();
        public ILuaScriptManagementService LuaScriptManagementService => _servicesProvider.GetService<ILuaScriptManagementService>();
        public INetworkingService NetworkingService => _servicesProvider.GetService<INetworkingService>();
        public IEventService EventService => _servicesProvider.GetService<IEventService>();
        public LuaGame Game => _servicesProvider.GetService<LuaGame>();
        
        internal IStorageService StorageService => _servicesProvider.GetService<IStorageService>();

        /// <summary>
        /// Whether C# plugin code is enabled.
        /// </summary>
        public bool IsCsEnabled
        {
            get => _isCsEnabled?.Value ?? false;
            internal set => _isCsEnabled?.TrySetValue(value);
        }

        private ISettingBase<bool> _isCsEnabled;

        /// <summary>
        /// Whether the popup error GUI should be hidden/suppressed.
        /// </summary>
        public bool DisableErrorGUIOverlay
        {
            get => _disableErrorGUIOverlay?.Value ?? false;
            internal set => _disableErrorGUIOverlay?.TrySetValue(value);
        }
        private ISettingBase<bool> _disableErrorGUIOverlay;

        /// <summary>
        /// Whether usernames are anonymized or show in logs. 
        /// </summary>
        public bool HideUserNamesInLogs
        {
            get => _hideUserNamesInLogs?.Value ?? false;
            internal set => _hideUserNamesInLogs?.TrySetValue(value);
        }
        private ISettingBase<bool> _hideUserNamesInLogs;

        /// <summary>
        /// The SteamId of the Workshop LuaCs CPackage in use, if available.
        /// </summary>
        public ulong LuaForBarotraumaSteamId
        {
            get => _luaForBarotraumaSteamId?.Value ?? 0;
            internal set => _luaForBarotraumaSteamId?.TrySetValue(value);
        }
        private ISettingBase<ulong> _luaForBarotraumaSteamId;

        /// <summary>
        /// Whether the maximum message size over the network should be restricted.
        /// </summary>
        public bool RestrictMessageSize
        {
            get => _restrictMessageSize?.Value ?? false;
            internal set => _restrictMessageSize?.TrySetValue(value);
        }
        private ISettingBase<bool> _restrictMessageSize;

        /// <summary>
        /// The local save path for all local data storage for mods.
        /// </summary>
        public string LocalDataSavePath
        {
            get => _localDataSavePath?.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "/Data/Mods");
            internal set => _localDataSavePath?.TrySetValue(value);
        }
        private ISettingBase<string> _localDataSavePath;

        void LoadLuaCsConfig()
        {
            var luaCsPackage = ContentPackageManager.EnabledPackages.Regular.FirstOrDefault(cp => cp.NameMatches("LuaCsForBarotrauma"), null)
                ?? ContentPackageManager.LocalPackages.FirstOrDefault(cp => cp.NameMatches("LuaCsForBarotrauma"))
                ?? ContentPackageManager.WorkshopPackages.FirstOrDefault(cp => cp.NameMatches("LuaCsForBarotrauma"));
            
            _isCsEnabled = 
                ConfigService.TryGetConfig<ISettingBase<bool>>(luaCsPackage, "IsCsEnabled", out var val1)
                    ? val1
                    : null;
            _disableErrorGUIOverlay =
                ConfigService.TryGetConfig<ISettingBase<bool>>(luaCsPackage, "DisableErrorGUIOverlay", out var val3)
                    ? val3
                    : null;
            _hideUserNamesInLogs =
                ConfigService.TryGetConfig<ISettingBase<bool>>(luaCsPackage, "HideUserNamesInLogs", out var val4)
                    ? val4
                    : null;
            _luaForBarotraumaSteamId =
                ConfigService.TryGetConfig<ISettingBase<ulong>>(luaCsPackage, "LuaForBarotraumaSteamId", out var val5)
                    ? val5
                    : null;
            _restrictMessageSize =
                ConfigService.TryGetConfig<ISettingBase<bool>>(luaCsPackage, "RestrictMessageSize", out var val7)
                    ? val7
                    : null;
            _localDataSavePath =
                ConfigService.TryGetConfig<ISettingBase<string>>(luaCsPackage, "LocalDataSavePath", out var val8)
                    ? val8
                    : null;
        }
        
        private IServicesProvider SetupServicesProvider()
        {
            var servicesProvider = new ServicesProvider();
            
            // Base Service
            servicesProvider.RegisterServiceType<ILoggerService, LoggerService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<PerformanceCounterService, PerformanceCounterService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IStorageService, StorageService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<ISafeStorageService, SafeStorageService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IEventService, EventService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceResolver<ILuaCsHook>(factory => factory.GetInstance<IEventService>() as ILuaCsHook);
            servicesProvider.RegisterServiceType<IPackageManagementService, PackageManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IAssemblyManagementService, PluginManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceResolver<IPluginManagementService>(factory => factory.GetInstance<IAssemblyManagementService>());
            servicesProvider.RegisterServiceType<ILuaScriptManagementService, LuaScriptManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IConfigService, ConfigService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<INetworkingService, NetworkingService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<INetworkIdProvider, NetworkingIdProvider>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<HarmonyEventPatchesService, HarmonyEventPatchesService>(ServiceLifetime.Singleton);

            // Extension/Sub Services
            servicesProvider.RegisterServiceType<IAssemblyLoaderService.IFactory, AssemblyLoader.Factory>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<ISettingsRegistrationProvider, SettingsEntryRegistrar>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IModConfigService, ModConfigService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo>, ModConfigFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo>, ModConfigFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>, ModConfigFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigInfo>, SettingsFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigProfileInfo>, SettingsFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<INetworkIdProvider, NetworkingIdProvider>(ServiceLifetime.Transient);
            
            // All Lua Extras
            servicesProvider.RegisterServiceType<IDefaultLuaRegistrar, DefaultLuaRegistrar>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaPatcher, LuaPatcherService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaUserDataService, LuaUserDataService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ISafeLuaUserDataService, SafeLuaUserDataService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaCsInfoProvider, LuaCsInfoProvider>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<ILuaScriptLoader, LuaScriptLoader>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<LuaGame, LuaGame>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaCsTimer, LuaCsTimer>(ServiceLifetime.Singleton);

            // service config data
            servicesProvider.RegisterServiceType<IStorageServiceConfig, StorageServiceConfig>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaScriptServicesConfig, LuaScriptServicesConfig>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IConfigServiceConfig, ConfigServiceConfig>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IPackageManagementServiceConfig, PackageManagementServiceConfig>(ServiceLifetime.Singleton);

            // gen IL
            servicesProvider.CompileAndRun();
            return servicesProvider;
        }
        
        #endregion

        #region StateMachine
        
        private RunState _runState;
        /// <summary>
        /// The current run state of all services managed by LuaCs. 
        /// </summary>
        public RunState CurrentRunState
        {
            get => _runState;
            private set => _runState = value;
        }
        
        private readonly StateMachine<RunState> _runStateMachine;

        public void OnEnabledPackageListChanged(CorePackage package, IEnumerable<RegularPackage> regularPackages)
        {
            ProcessEnabledPackageChanges(new []{ package }.Concat<ContentPackage>(regularPackages).ToImmutableArray());
        }

        public void OnReloadAllPackages()
        {
            if (CurrentRunState <= RunState.Unloaded)
            {
                return;
            }

            CoroutineManager.Invoke(() =>
            {
                var state = CurrentRunState;
                SetRunState(RunState.Unloaded);
                SetRunState(state);
            });
        }

        private void ProcessEnabledPackageChanges(ImmutableArray<ContentPackage> packages)
        {
            if (CurrentRunState < RunState.LoadedNoExec)
            {
                return;
            }
            
            var state = CurrentRunState;
            if (CurrentRunState > RunState.LoadedNoExec)
            {
                SetRunState(RunState.LoadedNoExec);
            }
            
            this.Logger.LogResults(PackageManagementService.SyncLoadedPackagesList(packages));
            SetRunState(state); // restore
        }
        
        private void SetRunState(RunState targetRunState)
        {
            if (CurrentRunState == targetRunState)
            {
                return;
            }
            _runStateMachine.GotoState(targetRunState);
        }

        private ImmutableArray<ContentPackage> GetEnabledPackagesList()
        {
            var enabledRegular = ContentPackageManager.EnabledPackages.Regular.ToImmutableArray<ContentPackage>();
            if (!enabledRegular.Any(
                    p => p.Name.Equals("LuaCsForBarotrauma", StringComparison.InvariantCultureIgnoreCase) 
                         || p.Name.Equals("Lua for Barotrauma", StringComparison.InvariantCultureIgnoreCase)))
            {
                var luaCs = ContentPackageManager.AllPackages.FirstOrDefault(
                    p => p.Name.Equals("LuaCsForBarotrauma", StringComparison.InvariantCultureIgnoreCase) 
                         || p.Name.Equals("Lua For Barotrauma", StringComparison.InvariantCultureIgnoreCase));
                if (luaCs is null)
                {
                    DebugConsole.ThrowError($"The 'LuaCsForBarotrauma' mod could not be found. Please subscribe to it and add it to the EnabledPackages List!", 
                        new NullReferenceException($"The 'LuaCsForBarotrauma' mod could not be found. Please subscribe to it and add it to the EnabledPackages List!"),
                        createMessageBox: true);
                    return enabledRegular;
                }

                enabledRegular = new[] { luaCs }.Concat(enabledRegular).ToImmutableArray();
            }
            
            return enabledRegular;
        }
        
        private StateMachine<RunState> SetupStateMachine() 
        {
            return new StateMachine<RunState>(false, RunState.Unloaded, onEnter: RunStateUnloaded_OnEnter, null)
                .AddState(RunState.LoadedNoExec, onEnter: RunStateLoadedNoExec_OnEnter, null)
                .AddState(RunState.Running, onEnter: RunStateRunning_OnEnter, RunStateRunning_OnExit);

            // ReSharper disable InconsistentNaming
            void RunStateUnloaded_OnEnter(State<RunState> currentState)
            {
                if (PackageManagementService.IsAnyPackageRunning())
                {
                    Logger.LogResults(PackageManagementService.StopRunningPackages());
                }

                if (PackageManagementService.IsAnyPackageLoaded())
                {
                    DisposeLuaCsConfig();
                    Logger.LogResults(PackageManagementService.UnloadAllPackages());
                }

                EventService.Reset();
                ConfigService.Reset();
                LuaScriptManagementService.Reset();
                PackageManagementService.Reset();
                NetworkingService.Reset();

                SubscribeToLuaCsEvents();

                CurrentRunState = RunState.Unloaded;
            }

            void RunStateLoadedNoExec_OnEnter(State<RunState> currentState)
            {
                if (PackageManagementService.IsAnyPackageRunning())
                {
                    Logger.LogResults(PackageManagementService.StopRunningPackages());
                }

                if (!PackageManagementService.IsAnyPackageLoaded())
                {
                    foreach (var registrationProvider in _servicesProvider.GetAllServices<ISettingsRegistrationProvider>())
                    {
                        registrationProvider.RegisterTypeProviders(ConfigService, null);
                    }
                    Logger.LogResults(PackageManagementService.LoadPackagesInfo(GetEnabledPackagesList()));
                    Logger.LogResults(ConfigService.LoadSavedConfigsValues());
                    LoadLuaCsConfig();
                    
                }

                CurrentRunState = RunState.LoadedNoExec;
            }
                
            void RunStateRunning_OnEnter(State<RunState> currentState)
            {
                if (!PackageManagementService.IsAnyPackageLoaded())
                {
                    foreach (var registrationProvider in _servicesProvider.GetAllServices<ISettingsRegistrationProvider>())
                    {
                        registrationProvider.RegisterTypeProviders(ConfigService, null);
                    }
                    Logger.LogResults(PackageManagementService.LoadPackagesInfo(GetEnabledPackagesList()));
                    Logger.LogResults(ConfigService.LoadSavedConfigsValues());
                    LoadLuaCsConfig();
                }

                if (!PackageManagementService.IsAnyPackageRunning())
                {
                    Logger.LogResults(PackageManagementService.ExecuteLoadedPackages(GetEnabledPackagesList(), IsCsEnabled));
                }

#if CLIENT
                // Technically not very accurate, but we want to call after we run mods anyway
                if (GameMain.Client != null)
                {
                    EventService.PublishEvent<IEventServerConnected>(static p => p.OnServerConnected());
                }
#endif
                CurrentRunState = RunState.Running;
            }
                
            void RunStateRunning_OnExit(State<RunState> currentState)
            {
                Logger.LogResults(PackageManagementService.StopRunningPackages());
            }
            // ReSharper restore InconsistentNaming
        }
        
        #endregion
        
        #region LegacyRedirects

        public ILuaCsHook Hook => this.EventService;
        public INetworkingService Networking => this.NetworkingService;

        #endregion

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
                ConfigService.Dispose();
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
        
        void DisposeLuaCsConfig()
        {
            _isCsEnabled = null;
            _disableErrorGUIOverlay = null;
            _hideUserNamesInLogs = null;
            _luaForBarotraumaSteamId = null;
            _restrictMessageSize = null;
        }
    }

    /// <summary>
    /// Specifies the current run state of the LuaCs Modding System.
    /// <b>[Important]Enum State values ordering must be in the form of (lower state) === (higher state)</b>
    /// </summary>
    public enum RunState : byte
    {
        /// <summary>
        /// No assets are loaded, code execution suspended.
        /// </summary>
        Unloaded = 0,   
        /// <summary>
        /// Loaded mod configs, settings and assets. No code execution.
        /// </summary>
        LoadedNoExec = 1,   
        /// <summary>
        /// All assets loaded, code execution is active.
        /// </summary>
        Running = 2         
    }
}

