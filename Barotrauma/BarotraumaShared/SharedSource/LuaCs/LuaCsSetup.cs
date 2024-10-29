using System;
using System.IO;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading;
using LuaCsCompatPatchFunc = Barotrauma.LuaCsPatch;
using System.Diagnostics;
using MoonSharp.VsCodeDebugger;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;
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

    partial class LuaCsSetup
    {
        public const string LuaSetupFile = "Lua/LuaSetup.lua";
        public const string VersionFile = "luacsversion.txt";
#if WINDOWS
        public static ContentPackageId LuaForBarotraumaId = new SteamWorkshopId(2559634234);
#elif LINUX
        public static ContentPackageId LuaForBarotraumaId = new SteamWorkshopId(2970628943);
#elif OSX
        public static ContentPackageId LuaForBarotraumaId = new SteamWorkshopId(2970890020);
#endif

        public static ContentPackageId CsForBarotraumaId = new SteamWorkshopId(2795927223);


        private const string configFileName = "LuaCsSetupConfig.xml";

#if SERVER
        public const bool IsServer = true;
        public const bool IsClient = false;
#else
        public const bool IsServer = false;
        public const bool IsClient = true;
#endif

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

        private static int executionNumber = 0;


        public Script Lua { get; private set; }
        public LuaScriptLoader LuaScriptLoader { get; private set; }

        public LuaGame Game { get; private set; }
        public LuaCsHook Hook { get; private set; }
        public LuaCsTimer Timer { get; private set; }
        public LuaCsNetworking Networking { get; private set; }
        public LuaCsSteam Steam { get; private set; }
        public LuaCsPerformanceCounter PerformanceCounter { get; private set; }

        // must be available at anytime
        private static AssemblyManager _assemblyManager;
        public static AssemblyManager AssemblyManager => _assemblyManager ??= new AssemblyManager();
        
        private CsPackageManager _pluginPackageManager;
        public CsPackageManager PluginPackageManager => _pluginPackageManager ??= new CsPackageManager(AssemblyManager, this);

        public LuaCsModStore ModStore { get; private set; }
        private LuaRequire require { get; set; }
        public LuaCsSetupConfig Config { get; private set; }
        public MoonSharpVsCodeDebugServer DebugServer { get; private set; }
        public bool IsInitialized { get; private set; }

        private bool ShouldRunCs
        {
            get
            {
#if SERVER
                if (GetPackage(CsForBarotraumaId, false, false) != null && GameMain.Server.ServerPeer is LidgrenServerPeer) { return true; }
#endif

                return Config.EnableCsScripting;
            }
        }

        public LuaCsSetup()
        {
            Script.GlobalOptions.Platform = new LuaPlatformAccessor();

            Hook = new LuaCsHook(this);
            ModStore = new LuaCsModStore();

            Game = new LuaGame();
            Networking = new LuaCsNetworking();
            DebugServer = new MoonSharpVsCodeDebugServer();

            ReadSettings();
        }
        
        [Obsolete("Use AssemblyManager::GetTypesByName()")]
        public static Type GetType(string typeName, bool throwOnError = false, bool ignoreCase = false)
        {
            return AssemblyManager.GetTypesByName(typeName).FirstOrDefault((Type)null);
        }

        public void ToggleDebugger(int port = 41912)
        {
            if (!GameMain.LuaCs.DebugServer.IsStarted)
            {
                DebugServer.Start();
                AttachDebugger();

                LuaCsLogger.Log($"Lua Debug Server started on port {port}.");
            }
            else
            {
                DetachDebugger();
                DebugServer.Stop();

                LuaCsLogger.Log($"Lua Debug Server stopped.");
            }
        }

        public void AttachDebugger()
        {
            DebugServer.AttachToScript(Lua, "Script", s =>
            {
                if (s.Name.StartsWith("LocalMods") || s.Name.StartsWith("Lua"))
                {
                    return Environment.CurrentDirectory + "/" + s.Name;
                }
                return s.Name;
            });
        }

        public void DetachDebugger() => DebugServer.Detach(Lua);

        public void ReadSettings()
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

        private DynValue DoFile(string file, Table globalContext = null, string codeStringFriendly = null)
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
            PluginPackageManager.UnloadPlugins();            
            
            // unregister types
            foreach (Type type in AssemblyManager.GetAllLoadedACLs().SelectMany(
                         acl => acl.AssembliesTypes.Select(kvp => kvp.Value)))
            {
                UserData.UnregisterType(type, true);
            }

            if (Lua?.Globals is not null)
            {
                Lua.Globals.Remove("CsPackageManager");
                Lua.Globals.Remove("AssemblyManager");
            }

            if (Thread.CurrentThread == GameMain.MainThread) 
            {
                Hook?.Call("stop");
            }

            if (Lua != null && DebugServer.IsStarted)
            {
                DebugServer.Detach(Lua);
            }

            Game?.Stop();

            Hook?.Clear();
            ModStore.Clear();
            LuaScriptLoader = null;
            Lua = null;
            
            // we can only unload assemblies after clearing ModStore/references.
            PluginPackageManager.Dispose();
#pragma warning disable CS0618
            ACsMod.LoadedMods.Clear();
#pragma warning restore CS0618
            
            Game = new LuaGame();
            Networking = new LuaCsNetworking();
            Timer = new LuaCsTimer();
            Steam = new LuaCsSteam();
            PerformanceCounter = new LuaCsPerformanceCounter();

            IsInitialized = false;
        }

        public void Initialize(bool forceEnableCs = false)
        {
            if (IsInitialized)
            {
                Stop();
            }

            IsInitialized = true;

            LuaCsLogger.LogMessage("Lua! Version " + AssemblyInfo.GitRevision);

            bool csActive = ShouldRunCs || forceEnableCs;

            LuaScriptLoader = new LuaScriptLoader();
            LuaScriptLoader.ModulePaths = new string[] { };

            RegisterLuaConverters();

            Lua = new Script(CoreModules.Preset_SoftSandbox | CoreModules.Debug | CoreModules.IO | CoreModules.OS_System);
            Lua.Options.DebugPrint = (o) => { LuaCsLogger.LogMessage(o); };
            Lua.Options.ScriptLoader = LuaScriptLoader;
            Lua.Options.CheckThreadAccess = false;
            Script.GlobalOptions.ShouldPCallCatchException = (Exception ex) => { return true; };

            require = new LuaRequire(Lua);

            Game = new LuaGame();
            Networking = new LuaCsNetworking();
            Timer = new LuaCsTimer();
            Steam = new LuaCsSteam();
            PerformanceCounter = new LuaCsPerformanceCounter();
            Hook.Initialize();
            ModStore.Initialize();
            Networking.Initialize();

            UserData.RegisterType<LuaCsLogger>();
            UserData.RegisterType<LuaCsConfig>();
            UserData.RegisterType<LuaCsSetupConfig>();
            UserData.RegisterType<LuaCsAction>();
            UserData.RegisterType<LuaCsFile>();
            UserData.RegisterType<LuaCsCompatPatchFunc>();
            UserData.RegisterType<LuaCsPatchFunc>();
            UserData.RegisterType<LuaGame>();
            UserData.RegisterType<LuaCsTimer>();
            UserData.RegisterType<LuaCsFile>();
            UserData.RegisterType<LuaCsNetworking>();
            UserData.RegisterType<LuaCsSteam>();
            UserData.RegisterType<LuaUserData>();
            UserData.RegisterType<LuaCsPerformanceCounter>();
            UserData.RegisterType<IUserDataDescriptor>();

            Lua.Globals["printerror"] = (DynValue o) => { LuaCsLogger.LogError(o.ToString(), LuaCsMessageOrigin.LuaMod); };

            Lua.Globals["setmodulepaths"] = (Action<string[]>)SetModulePaths;

            Lua.Globals["dofile"] = (Func<string, Table, string, DynValue>)DoFile;
            Lua.Globals["loadfile"] = (Func<string, Table, string, DynValue>)LoadFile;
            Lua.Globals["require"] = (Func<string, Table, DynValue>)require.Require;

            Lua.Globals["dostring"] = (Func<string, Table, string, DynValue>)Lua.DoString;
            Lua.Globals["load"] = (Func<string, Table, string, DynValue>)Lua.LoadString;

            Lua.Globals["Logger"] = UserData.CreateStatic<LuaCsLogger>();
            Lua.Globals["LuaUserData"] = UserData.CreateStatic<LuaUserData>();
            Lua.Globals["Game"] = Game;
            Lua.Globals["Hook"] = Hook;
            Lua.Globals["ModStore"] = ModStore;
            Lua.Globals["Timer"] = Timer;
            Lua.Globals["File"] = UserData.CreateStatic<LuaCsFile>();
            Lua.Globals["Networking"] = Networking;
            Lua.Globals["Steam"] = Steam;
            Lua.Globals["PerformanceCounter"] = PerformanceCounter;
            Lua.Globals["LuaCsConfig"] = new LuaCsSetupConfig(Config);

            Lua.Globals["ExecutionNumber"] = executionNumber;
            Lua.Globals["CSActive"] = csActive;

            Lua.Globals["SERVER"] = IsServer;
            Lua.Globals["CLIENT"] = IsClient;

            if (DebugServer.IsStarted)
            {
                AttachDebugger();
            }

            if (csActive)
            {
                LuaCsLogger.LogMessage("Cs! Version " + AssemblyInfo.GitRevision);

                UserData.RegisterType<CsPackageManager>();
                UserData.RegisterType<AssemblyManager>();
                UserData.RegisterType<IAssemblyPlugin>();

                Lua.Globals["PluginPackageManager"] = PluginPackageManager;
                Lua.Globals["AssemblyManager"] = AssemblyManager;
                
                try
                {
                    Stopwatch taskTimer = new();
                    taskTimer.Start();
                    ModStore.Clear();
                    
                    var state = PluginPackageManager.LoadAssemblyPackages();
                    if (state is AssemblyLoadingSuccessState.Success or AssemblyLoadingSuccessState.AlreadyLoaded)
                    {
                        if(!PluginPackageManager.PluginsInitialized)
                            PluginPackageManager.InstantiatePlugins(true);
                        if(!PluginPackageManager.PluginsPreInit)
                            PluginPackageManager.RunPluginsPreInit();   // this is intended to be called at startup in the future
                        if(!PluginPackageManager.PluginsLoaded)
                            PluginPackageManager.RunPluginsInit();
                        state = AssemblyLoadingSuccessState.Success;
                        taskTimer.Stop();
                        ModUtils.Logging.PrintMessage($"{nameof(LuaCsSetup)}: Completed assembly loading. Total time {taskTimer.ElapsedMilliseconds}ms.");
                    }
                    else
                    {
                        PluginPackageManager.Dispose(); // cleanup if there's an error
                    }
                    
                    if(state is not AssemblyLoadingSuccessState.Success)
                    {
                        ModUtils.Logging.PrintError($"{nameof(LuaCsSetup)}: Error while loading Cs-Assembly Mods | Err: {state}");
                        taskTimer.Stop();
                    }
                }
                catch (Exception e)
                {
                    ModUtils.Logging.PrintError($"{nameof(LuaCsSetup)}::{nameof(Initialize)}() | Error while loading assemblies! Details: {e.Message} | {e.StackTrace}");
                }
            }


            ContentPackage luaPackage = GetPackage(LuaForBarotraumaId);

            void RunLocal()
            {
                LuaCsLogger.LogMessage("Using LuaSetup.lua from the Barotrauma Lua/ folder.");
                string luaPath = LuaSetupFile;
                CallLuaFunction(Lua.LoadFile(luaPath), Path.GetDirectoryName(Path.GetFullPath(luaPath)));
            }

            void RunWorkshop()
            {
                LuaCsLogger.LogMessage("Using LuaSetup.lua from the content package.");
                string luaPath = Path.Combine(Path.GetDirectoryName(luaPackage.Path), "Binary/Lua/LuaSetup.lua");
                CallLuaFunction(Lua.LoadFile(luaPath), Path.GetDirectoryName(Path.GetFullPath(luaPath)));
            }

            void RunNone()
            {
                LuaCsLogger.LogError("LuaSetup.lua not found! Lua/LuaSetup.lua, no Lua scripts will be executed or work.", LuaCsMessageOrigin.LuaMod);
            }

            if (Config.PreferToUseWorkshopLuaSetup)
            {
                if (luaPackage != null) { RunWorkshop(); }
                else if (File.Exists(LuaSetupFile)) { RunLocal(); }
                else { RunNone(); }
            }
            else
            {
                if (File.Exists(LuaSetupFile)) { RunLocal(); }
                else if (luaPackage != null) { RunWorkshop(); }
                else { RunNone(); }
            }

#if SERVER
            GameMain.Server.ServerSettings.LoadClientPermissions();
#endif

            executionNumber++;
        }
    }
}
