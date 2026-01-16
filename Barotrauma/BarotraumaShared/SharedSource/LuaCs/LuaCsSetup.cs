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

    partial class LuaCsSetup : IDisposable, IEventScreenSelected, IEventAllPackageListChanged, IEventEnabledPackageListChanged, 
        IEventReloadAllPackages
    {
        public LuaCsSetup()
        {
            // == startup
            _servicesProvider = new ServicesProvider();
            RegisterServices(_servicesProvider);
            if (!ValidateLuaCsContent())
            {
                Logger.LogError($"{nameof(LuaCsSetup)}: ModConfigXml missing. Unable to continue.");
                throw new ApplicationException($"{nameof(LuaCsSetup)}: Lua ModConfig.xml is missing. Unable to continue.");
            }
            SubscribeToLuaCsEvents();
            _runStateMachine = SetupStateMachine();
            //LoadLuaCsConfig();
            
            return;
            // == end
            
            StateMachine<RunState> SetupStateMachine() 
            {
                return new StateMachine<RunState>(false, RunState.Unloaded, RunStateUnloaded_OnEnter, null)
                    .AddState(RunState.LoadedNoExec, RunStateLoadedNoExec_OnEnter, RunStateLoadedNoExec_OnExit)
                    .AddState(RunState.Running, RunStateRunning_OnEnter, RunStateRunning_OnExit);

                // ReSharper disable InconsistentNaming
                void RunStateUnloaded_OnEnter(State<RunState> currentState)
                {
                    
                }

                void RunStateLoadedNoExec_OnEnter(State<RunState> currentState)
                {
                    
                }
                
                void RunStateLoadedNoExec_OnExit(State<RunState> currentState)
                {
                    
                }
                
                void RunStateRunning_OnEnter(State<RunState> currentState)
                {
                    
                }
                
                void RunStateRunning_OnExit(State<RunState> currentState)
                {
                    
                }
                // ReSharper restore InconsistentNaming
            }
        }
        
        bool ValidateLuaCsContent()
        {
#if DEBUG
            // TODO: we just wanna boot for now
            return true;
#endif
            // check if /Content/Lua/ModConfig.xml exists
            // if not, try to copy it from the Local Mods folder 
            // if not, try to copy it from the Workshop Mods folder
            // if that fails, throw an error and exit.
            throw new NotImplementedException();
        }
        
        void RegisterServices(IServicesProvider servicesProvider)
        {
            servicesProvider.RegisterServiceType<ILoggerService, LoggerService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<PerformanceCounterService, PerformanceCounterService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IStorageService, StorageService>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<IEventService, EventService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IPackageManagementService, PackageManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IPluginManagementService, PluginManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaScriptManagementService, LuaScriptManagementService>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaScriptLoader, LuaScriptLoader>(ServiceLifetime.Transient);
            servicesProvider.RegisterServiceType<LuaGame, LuaGame>(ServiceLifetime.Singleton);
            
            // TODO: IConfigService
            // TODO: INetworkingService
            // TODO: [Resource Converter/Parser Services]
            
            servicesProvider.RegisterServiceType<IModConfigService, ModConfigService>(ServiceLifetime.Transient);
            
            // service config data
            servicesProvider.RegisterServiceType<IStorageServiceConfig, StorageServiceConfig>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<ILuaScriptServicesConfig, LuaScriptServicesConfig>(ServiceLifetime.Singleton);
            servicesProvider.RegisterServiceType<IConfigServiceConfig, ConfigServiceConfig>(ServiceLifetime.Singleton);
            
            // gen IL
            servicesProvider.Compile();
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
#else
        public const bool IsServer = false;
#endif
        public const bool IsClient = !IsServer;

        #endregion
        
        #region Services_ConfigVars
        
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

        
        #region LuaCsInternal

        
        /*
         * === Config Vars
         */
        
        /// <summary>
        /// Whether C# plugin code is enabled.
        /// </summary>
        internal IConfigEntry<bool> IsCsEnabled { get; private set; }
        
        /// <summary>
        /// Whether mods marked as 'forced' or 'always load' should only be loaded if they're in the enabled mods list.
        /// </summary>
        internal IConfigEntry<bool> TreatForcedModsAsNormal { get; private set; }
        
        /// <summary>
        /// Whether the lua script runner from Workshop package should be used over the in-built version.
        /// </summary>
        internal IConfigEntry<bool> PreferToUseWorkshopLuaSetup { get; private set; }
        
        /// <summary>
        /// Whether the popup error GUI should be hidden/suppressed.
        /// </summary>
        internal IConfigEntry<bool> DisableErrorGUIOverlay { get; private set; }
        
        /// <summary>
        /// Whether usernames are anonymized or show in logs. 
        /// </summary>
        internal IConfigEntry<bool> HideUserNamesInLogs { get; private set; }
        
        /// <summary>
        /// The SteamId of the Workshop LuaCs CPackage in use, if available.
        /// </summary>
        internal IConfigEntry<ulong> LuaForBarotraumaSteamId { get; private set; }
        
        /// <summary>
        /// TODO: @evilfactory@users.noreply.github.com
        /// </summary>
        internal IConfigEntry<bool> RestrictMessageSize { get; private set; }
        
        /// <summary>
        /// The local save path for all local data storage for mods.
        /// </summary>
        internal IConfigEntry<string> LocalDataSavePath { get; private set; }

        #endregion

        /**
         * == Ops Vars
         */
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
        private readonly ConcurrentQueue<ContentPackage> _toLoad = new();
        private readonly ConcurrentQueue<ContentPackage> _toUnload = new();
        
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
            throw new NotImplementedException($"Rewrite the loading state system.");
            
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
            
            //ProcessPackagesListDifferences();
        }
        
        void SetRunState(RunState newRunState)
        {
            if (CurrentRunState == newRunState)
                return;
            
        }

        void LoadLuaCsConfig()
        {
            IsCsEnabled = ConfigService.TryGetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "IsCsEnabled", out var val1) ? val1
                          : throw new NullReferenceException($"{nameof(IsCsEnabled)} cannot be loaded.");
            TreatForcedModsAsNormal = ConfigService.TryGetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "TreatForcedModsAsNormal", out var val2) ? val2
                                      : throw new NullReferenceException($"{nameof(TreatForcedModsAsNormal)} cannot be loaded.");
            DisableErrorGUIOverlay = ConfigService.TryGetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "DisableErrorGUIOverlay", out var val3) ? val3
                                     : throw new NullReferenceException($"{nameof(DisableErrorGUIOverlay)} cannot be loaded.");
            HideUserNamesInLogs = ConfigService.TryGetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "HideUserNamesInLogs", out var val4) ? val4
                                  : throw new NullReferenceException($"{nameof(HideUserNamesInLogs)} cannot be loaded.");
            LuaForBarotraumaSteamId = ConfigService.TryGetConfig<IConfigEntry<ulong>>(ContentPackageManager.VanillaCorePackage, "LuaForBarotraumaSteamId", out var val5) ? val5 
                                      : throw new NullReferenceException($"{nameof(LuaForBarotraumaSteamId)} cannot be loaded.");
            RestrictMessageSize = ConfigService.TryGetConfig<IConfigEntry<bool>>(ContentPackageManager.VanillaCorePackage, "RestrictMessageSize", out var val7) ? val7
                                  : throw new NullReferenceException($"{nameof(RestrictMessageSize)} cannot be loaded.");
        }

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

