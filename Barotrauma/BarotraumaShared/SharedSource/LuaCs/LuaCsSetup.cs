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
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Services;
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

        protected ILoggerService Logger { get; private set; }

        private IServicesProvider servicesProvider;

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
        private LuaRequire Require { get; set; }
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
            servicesProvider = new ServicesProvider();
            servicesProvider.RegisterServiceType<ILoggerService, LoggerService>(ServiceLifetime.Singleton);

            if (servicesProvider.TryGetService(out ILoggerService logger))
            {
                Logger = logger;
            }
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


            IsInitialized = false;
        }

        public void Initialize(bool forceEnableCs = false)
        {
            if (IsInitialized)
            {
                Stop();
            }

            IsInitialized = true;

            Logger.Log($"Initializing LuaCs, git revision = {AssemblyInfo.GitRevision}.");


        }
    }
}
