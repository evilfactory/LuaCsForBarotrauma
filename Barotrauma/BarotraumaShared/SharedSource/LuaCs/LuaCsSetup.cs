using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Processing;
using Barotrauma.LuaCs.Services.Safe;
using Barotrauma.Networking;
using Barotrauma.Steam;
using FluentResults;
using ImpromptuInterface;
using Microsoft.Toolkit.Diagnostics;

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
            if (!ValidateLuaCsContent())
            {
                Logger.LogError($"{nameof(LuaCsSetup)}: ModConfig.xml missing. Unable to continue.");
                throw new ApplicationException($"{nameof(LuaCsSetup)}: Lua's ModConfig.xml is missing. Unable to continue.");
            }
            _runStateMachine = SetupStateMachine();
            SubscribeToLuaCsEvents();
            SetRunState(RunState.LoadedNoExec);
            LoadLuaCsConfig();
        }
        
        bool ValidateLuaCsContent()
        {
#if DEBUG
            // TODO: we just wanna boot for now
            return true;
#endif
            // check if /Content/ModConfig.xml exists
            // if not, try to copy missing files from the Local Mods folder 
            // if not, try to copy missing files from the Workshop Mods folder
            // if that fails, throw an error and exit.
            throw new NotImplementedException();
        }
        
        void SubscribeToLuaCsEvents()
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
        internal ISettingEntry<bool> IsCsEnabled { get; private set; }
        
        /// <summary>
        /// Whether mods marked as 'forced' or 'always load' should only be loaded if they're in the enabled mods list.
        /// </summary>
        internal ISettingEntry<bool> TreatForcedModsAsNormal { get; private set; }
        
        /// <summary>
        /// Whether the lua script runner from Workshop package should be used over the in-built version.
        /// </summary>
        internal ISettingEntry<bool> PreferToUseWorkshopLuaSetup { get; private set; }
        
        /// <summary>
        /// Whether the popup error GUI should be hidden/suppressed.
        /// </summary>
        internal ISettingEntry<bool> DisableErrorGUIOverlay { get; private set; }
        
        /// <summary>
        /// Whether usernames are anonymized or show in logs. 
        /// </summary>
        internal ISettingEntry<bool> HideUserNamesInLogs { get; private set; }
        
        /// <summary>
        /// The SteamId of the Workshop LuaCs CPackage in use, if available.
        /// </summary>
        internal ISettingEntry<ulong> LuaForBarotraumaSteamId { get; private set; }
        
        /// <summary>
        /// TODO: @evilfactory@users.noreply.github.com
        /// </summary>
        internal ISettingEntry<bool> RestrictMessageSize { get; private set; }
        
        /// <summary>
        /// The local save path for all local data storage for mods.
        /// </summary>
        internal ISettingEntry<string> LocalDataSavePath { get; private set; }

        void LoadLuaCsConfig()
        {
            IsCsEnabled = ConfigService.TryGetConfig<ISettingEntry<bool>>(ContentPackageManager.VanillaCorePackage, "IsCsEnabled", out var val1) ? val1
                : throw new NullReferenceException($"{nameof(IsCsEnabled)} cannot be loaded.");
            TreatForcedModsAsNormal = ConfigService.TryGetConfig<ISettingEntry<bool>>(ContentPackageManager.VanillaCorePackage, "TreatForcedModsAsNormal", out var val2) ? val2
                : throw new NullReferenceException($"{nameof(TreatForcedModsAsNormal)} cannot be loaded.");
            DisableErrorGUIOverlay = ConfigService.TryGetConfig<ISettingEntry<bool>>(ContentPackageManager.VanillaCorePackage, "DisableErrorGUIOverlay", out var val3) ? val3
                : throw new NullReferenceException($"{nameof(DisableErrorGUIOverlay)} cannot be loaded.");
            HideUserNamesInLogs = ConfigService.TryGetConfig<ISettingEntry<bool>>(ContentPackageManager.VanillaCorePackage, "HideUserNamesInLogs", out var val4) ? val4
                : throw new NullReferenceException($"{nameof(HideUserNamesInLogs)} cannot be loaded.");
            LuaForBarotraumaSteamId = ConfigService.TryGetConfig<ISettingEntry<ulong>>(ContentPackageManager.VanillaCorePackage, "LuaForBarotraumaSteamId", out var val5) ? val5 
                : throw new NullReferenceException($"{nameof(LuaForBarotraumaSteamId)} cannot be loaded.");
            RestrictMessageSize = ConfigService.TryGetConfig<ISettingEntry<bool>>(ContentPackageManager.VanillaCorePackage, "RestrictMessageSize", out var val7) ? val7
                : throw new NullReferenceException($"{nameof(RestrictMessageSize)} cannot be loaded.");
        }
        
        private IServicesProvider SetupServicesProvider()
        {
            var servicesProvider = new ServicesProvider();
            
            servicesProvider.RegisterServiceType<ILoggerService, LoggerService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<PerformanceCounterService, PerformanceCounterService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IStorageService, StorageService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<ISafeStorageService, SafeStorageService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IEventService, EventService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaCsHook, EventService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IPackageManagementService, PackageManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IPluginManagementService, PluginManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaScriptManagementService, LuaScriptManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaScriptLoader, LuaScriptLoader>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<LuaGame, LuaGame>(ServiceLifetime.Singleton);
            // TODO: INetworkingService
            servicesProvider.RegisterServiceType<IConfigService, ConfigService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IModConfigService, ModConfigService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo>, ModConfigFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo>, ModConfigFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>, ModConfigFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigInfo>, SettingsFileParserService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigProfileInfo>, SettingsFileParserService>(ServiceLifetime.Transient);
            // service config data
            servicesProvider.RegisterServiceType<IStorageServiceConfig, StorageServiceConfig>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaScriptServicesConfig, LuaScriptServicesConfig>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IConfigServiceConfig, ConfigServiceConfig>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IPackageManagementServiceConfig, PackageManagementServiceConfig>(ServiceLifetime.Singleton);
            // gen IL
            servicesProvider.Compile();
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
            
            var state = CurrentRunState;
            SetRunState(RunState.Unloaded);
            SetRunState(state);
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
            
            PackageManagementService.SyncLoadedPackagesList(packages);
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
        
        private StateMachine<RunState> SetupStateMachine() 
        {
            return new StateMachine<RunState>(false, RunState.Unloaded, RunStateUnloaded_OnEnter, null)
                .AddState(RunState.LoadedNoExec, RunStateLoadedNoExec_OnEnter, null)
                .AddState(RunState.Running, RunStateRunning_OnEnter, RunStateRunning_OnExit);

            // ReSharper disable InconsistentNaming
            void RunStateUnloaded_OnEnter(State<RunState> currentState)
            {
                if (PackageManagementService.IsAnyPackageRunning())
                {
                    Logger.LogResults(PackageManagementService.StopRunningPackages());
                }

                if (PackageManagementService.IsAnyPackageRunning())
                {
                    DisposeLuaCsConfig();
                    Logger.LogResults(PackageManagementService.UnloadAllPackages());
                }
                
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
                    Logger.LogResults(PackageManagementService.LoadPackagesInfo(ContentPackageManager.EnabledPackages.All.ToImmutableArray()));
                    LoadLuaCsConfig();
                }

                CurrentRunState = RunState.LoadedNoExec;
            }
                
            void RunStateRunning_OnEnter(State<RunState> currentState)
            {
                if (!PackageManagementService.IsAnyPackageLoaded())
                {
                    Logger.LogResults(PackageManagementService.LoadPackagesInfo(ContentPackageManager.EnabledPackages.All.ToImmutableArray()));
                    LoadLuaCsConfig();
                }

                if (!PackageManagementService.IsAnyPackageRunning())
                {
                    Logger.LogResults(PackageManagementService.ExecuteLoadedPackages(ContentPackageManager.EnabledPackages.All.ToImmutableArray()));
                }
                
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
            IsCsEnabled = null;
            TreatForcedModsAsNormal = null;
            DisableErrorGUIOverlay = null;
            HideUserNamesInLogs = null;
            LuaForBarotraumaSteamId = null;
            RestrictMessageSize = null;
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

