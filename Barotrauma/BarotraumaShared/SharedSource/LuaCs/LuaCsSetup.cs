using System;
using System.IO;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Services;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Processing;
using Barotrauma.Networking;

namespace Barotrauma
{
    class LuaCsSetupConfig
    {
        public bool EnableCsScripting = false;
        public bool TreatForcedModsAsNormal = true;
        public bool PreferToUseWorkshopLuaSetup = false;
        public bool DisableErrorGUIOverlay = false;
        public bool HideUserNames
        {
            get { return LuaCsLogger.HideUserNames; }
            set { LuaCsLogger.HideUserNames = value; }
        }

        public LuaCsSetupConfig() { }
        public LuaCsSetupConfig(LuaCsSetupConfig config)
        {
            EnableCsScripting = config.EnableCsScripting;
            TreatForcedModsAsNormal = config.TreatForcedModsAsNormal;
            PreferToUseWorkshopLuaSetup = config.PreferToUseWorkshopLuaSetup;
            DisableErrorGUIOverlay = config.DisableErrorGUIOverlay;
        }
    }

    internal delegate void LuaCsMessageLogger(string message);
    internal delegate void LuaCsErrorHandler(Exception ex, LuaCsMessageOrigin origin);
    internal delegate void LuaCsExceptionHandler(Exception ex, LuaCsMessageOrigin origin);

    partial class LuaCsSetup : IDisposable
    {
        public LuaCsSetup()
        {
            // load services
            _servicesProvider = new ServicesProvider();
            RegisterServices();
            
            // load manifest
            if (!_servicesProvider.TryGetService(out IModConfigParserService modConfigSvc))
                throw new NullReferenceException("LuaCsSetup: Failed to get mod config parser service!");   // we should crash here
            var luaConfig = modConfigSvc.BuildConfigFromManifest(Directory.GetCurrentDirectory() + LuaCsConfigFile);
            if (!luaConfig.IsSuccess)
            {
                Logger.LogResults(luaConfig.ToResult());
                throw new FileLoadException("LuaCsSetup: Failed to load config file!");
            } 
            
            // load resources
            RegisterLocalizations();
            RegisterConfigs();

            LuaForBarotraumaId = new SteamWorkshopId(LuaForBarotraumaSteamId.Value);
            
            return;
            //---
            
            void RegisterServices()
            {
                _servicesProvider.RegisterServiceType<ILoggerService, LoggerService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<PerformanceCounterService, PerformanceCounterService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<IPackageService, PackageService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<IPackageManagementService, PackageManagementService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<ILuaScriptService, LuaScriptService>(ServiceLifetime.Singleton);
                _servicesProvider.RegisterServiceType<ILuaScriptManagementService, LuaScriptService>(ServiceLifetime.Singleton);
                // TODO: IConfigService
                // TODO: INetworkingService
                // TODO: [Resource Converter/Parser Services]
                // TODO: ILocalizationService
                // TODO: IEventService
                _servicesProvider.Compile();
            }

            void RegisterLocalizations()
            {
                LocalizationService.LoadLocalizations(luaConfig.Value.Localizations);
            }

            void RegisterConfigs()
            {
                if (ConfigService.AddConfigs(luaConfig.Value.Configs) is { IsSuccess: false } res1)
                {
                    Logger.LogResults(res1);
                    throw new Exception("LuaCsSetup: Failed to load config!");
                }
                
                if (ConfigService.AddConfigsProfiles(luaConfig.Value.ConfigProfiles) is { IsSuccess: false } res2)
                {
                    Logger.LogResults(res2);
                    throw new Exception("LuaCsSetup: Failed to load config profiles!");
                }

                IsCsEnabled = GetOrThrowForConfig<bool>(luaConfig.Value.PackageName, "IsCsEnabled");
                TreatForcedModsAsNormal = GetOrThrowForConfig<bool>(luaConfig.Value.PackageName, "TreatForcedModsAsNormal");
                PreferToUseWorkshopLuaSetup = GetOrThrowForConfig<bool>(luaConfig.Value.PackageName, "PreferToUseWorkshopLuaSetup");
                DisableErrorGUIOverlay = GetOrThrowForConfig<bool>(luaConfig.Value.PackageName, "DisableErrorGUIOverlay");
                EnableThreadedLoading = GetOrThrowForConfig<bool>(luaConfig.Value.PackageName, "EnableThreadedLoading");
                HideUserNamesInLogs = GetOrThrowForConfig<bool>(luaConfig.Value.PackageName, "HideUserNamesInLogs");
                LuaForBarotraumaSteamId = GetOrThrowForConfig<ulong>(luaConfig.Value.PackageName, "LuaForBarotraumaSteamId");
                
                return;
                //---
                
                IConfigEntry<T> GetOrThrowForConfig<T>(string packName, string internalName) where T : IConvertible, IEquatable<T>
                {
                    var cfgRes = ConfigService.GetConfig<IConfigEntry<T>>(packName, internalName);
                    if (cfgRes.IsSuccess)
                    {
                        return cfgRes.Value;
                    }
                    Logger.LogResults(cfgRes.ToResult());
                    throw new Exception($"LuaCsSetup: Failed to load config for {internalName}!");
                }
            }
            
            
        }
        
        #region CONST_DEF

        public const string LuaCsConfigFile = "LuaCsConfig.xml";
        
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
        public ILuaScriptManagementService LuaScriptService => _servicesProvider.TryGetService<ILuaScriptManagementService>(out var svc) 
            ? svc : throw new NullReferenceException("Lua Script Manager service not found!");
        public ILocalizationService LocalizationService => _servicesProvider.TryGetService<ILocalizationService>(out var svc) 
            ? svc : throw new NullReferenceException("Localization Manager service not found!");
        public INetworkingService NetworkingService => _servicesProvider.TryGetService<INetworkingService>(out var svc) 
            ? svc : throw new NullReferenceException("Networking Manager service not found!");
        public IEventService EventService => _servicesProvider.TryGetService<IEventService>(out var svc) 
            ? svc : throw new NullReferenceException("Networking Manager service not found!");

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
        /// [Experimental] Whether multithreading should be used for loading. 
        /// </summary>
        public IConfigEntry<bool> EnableThreadedLoading { get; private set; }
        
        /// <summary>
        /// Whether usernames are anonymized or show in logs. 
        /// </summary>
        public IConfigEntry<bool> HideUserNamesInLogs { get; private set; }
        
        private IConfigEntry<ulong> LuaForBarotraumaSteamId { get; set; } 
        
        #endregion

        #region LegacyRedirects

        public ILuaCsHook Hook => this.EventService;
        

        #endregion

        /// <summary>
        /// Whether mod content is loaded and being executed.
        /// </summary>
        public bool IsModContentRunning { get; private set; }

        public readonly ContentPackageId LuaForBarotraumaId;

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

        /*public Script Lua { get; private set; }
        public LuaScriptLoader LuaScriptLoader { get; private set; }

        public LuaGame Game { get; private set; }
        public LuaCsHook Hook { get; private set; }
        public LuaCsTimer Timer { get; private set; }
        public LuaCsNetworking Networking { get; private set; }
        public LuaCsSteam Steam { get; private set; }

        // must be available at anytime
        private static AssemblyManager _assemblyManager;
        public static AssemblyManager AssemblyManager => _assemblyManager ??= new AssemblyManager();
        
        private CsPackageManager _pluginPackageManager;
        public CsPackageManager PluginPackageManager => _pluginPackageManager ??= new CsPackageManager(AssemblyManager, this);
        private LuaRequire Require { get; set; }
        public LuaCsSetupConfig Config { get; private set; }
        public MoonSharpVsCodeDebugServer DebugServer { get; private set; }
        public bool IsInitialized { get; private set; }*/

        private bool ShouldRunCs
        {
            get
            {
#if SERVER
                if (GetPackage(CsForBarotraumaId, false, false) != null && GameMain.Server.ServerPeer is LidgrenServerPeer) { return true; }
#endif
                return IsCsEnabled.Value;
            }
        }

        

        [Obsolete("Use AssemblyManager::GetTypesByName()")]
        public static Type GetType(string typeName, bool throwOnError = false, bool ignoreCase = false)
        {
            throw new NotImplementedException();
            //return AssemblyManager.GetTypesByName(typeName).FirstOrDefault((Type)null);
        }
        
        // Old config ref
        /*public void ReadSettings()
        {
            Config = new LuaCsSetupConfig();

            if (File.Exists(configFileName))
            {
                try
                {
                    using (var file = File.Open(configFileName, FileMode.Open, FileAccess.Read))
                    {
                        XDocument document = XDocument.Load(file);
                        Config.EnableCsScripting = document.Root.GetAttributeBool("EnableCsScripting", Config.EnableCsScripting);
                        Config.TreatForcedModsAsNormal = document.Root.GetAttributeBool("TreatForcedModsAsNormal", Config.TreatForcedModsAsNormal);
                        Config.PreferToUseWorkshopLuaSetup = document.Root.GetAttributeBool("PreferToUseWorkshopLuaSetup", Config.PreferToUseWorkshopLuaSetup);
                        Config.DisableErrorGUIOverlay = document.Root.GetAttributeBool("DisableErrorGUIOverlay", Config.DisableErrorGUIOverlay);
                        Config.HideUserNames = document.Root.GetAttributeBool("HideUserNames", Config.HideUserNames);
                    }
                }
                catch (Exception e)
                {
                    LuaCsLogger.HandleException(e, LuaCsMessageOrigin.LuaCs);
                }
            }
        }

        public void WriteSettings()
        {
            XDocument document = new XDocument();
            document.Add(new XElement("LuaCsSetupConfig"));
            document.Root.SetAttributeValue("EnableCsScripting", Config.EnableCsScripting);
            document.Root.SetAttributeValue("EnableCsScripting", Config.EnableCsScripting);
            document.Root.SetAttributeValue("TreatForcedModsAsNormal", Config.TreatForcedModsAsNormal);
            document.Root.SetAttributeValue("PreferToUseWorkshopLuaSetup", Config.PreferToUseWorkshopLuaSetup);
            document.Root.SetAttributeValue("DisableErrorGUIOverlay", Config.DisableErrorGUIOverlay);
            document.Root.SetAttributeValue("HideUserNames", Config.HideUserNames);
            document.Save(configFileName);
        }*/

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

        // Old code ref
        /*private DynValue DoFile(string file, Table globalContext = null, string codeStringFriendly = null)
        {
            if (!LuaCsFile.CanReadFromPath(file))
            {
                throw new ScriptRuntimeException($"dofile: File access to {file} not allowed.");
            }

            if (!LuaCsFile.Exists(file))
            {
                throw new ScriptRuntimeException($"dofile: File {file} not found.");
            }

            return Lua.DoFile(file, globalContext, codeStringFriendly);
        }

        private DynValue LoadFile(string file, Table globalContext = null, string codeStringFriendly = null)
        {
            if (!LuaCsFile.CanReadFromPath(file))
            {
                throw new ScriptRuntimeException($"loadfile: File access to {file} not allowed.");
            }

            if (!LuaCsFile.Exists(file))
            {
                throw new ScriptRuntimeException($"loadfile: File {file} not found.");
            }

            return Lua.LoadFile(file, globalContext, codeStringFriendly);
        }

        public DynValue CallLuaFunction(object function, params object[] args)
        {
            // XXX: `lua` might be null if `LuaCsSetup.Stop()` is called while
            // a patched function is still running.
            if (Lua == null) { return null; }

            lock (Lua)
            {
                try
                {
                    return Lua.Call(function, args);
                }
                catch (Exception e)
                {
                    LuaCsLogger.HandleException(e, LuaCsMessageOrigin.LuaMod);
                }
                return null;
            }
        }

        private void SetModulePaths(string[] str)
        {
            LuaScriptLoader.ModulePaths = str;
        }

        public void Update()
        {
            Timer?.Update();
            Steam?.Update();

#if CLIENT
            Stopwatch luaSw = new Stopwatch();
            luaSw.Start();
#endif
            Hook?.Call("think");
#if CLIENT
            luaSw.Stop();
            GameMain.PerformanceCounter.AddElapsedTicks("Think Hook", luaSw.ElapsedTicks);
#endif
        }

        
        public void Stop()
        {


            IsInitialized = false;
        }

        public void Initialize(bool forceEnableCs = false)
        {
            if (IsInitialized)
            {
                Stop();
            }

            IsInitialized = true;

            Logger.Log($"Initializing LuaCs, git revision = {AssemblyInfo.GitRevision}");
        }*/
        
        public void Update()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }
    }
}
