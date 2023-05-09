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

[assembly: InternalsVisibleTo(Barotrauma.CsScriptBase.LegacyCsScriptAssembly, AllInternalsVisible = true)]
namespace Barotrauma
{
    class LuaCsSetupConfig
    {
        public bool FirstTimeCsWarning = true;
        public bool ForceCsScripting = false;
        public bool TreatForcedModsAsNormal = false;
        public bool PreferToUseWorkshopLuaSetup = false;
        public bool DisableErrorGUIOverlay = false;

        public LuaCsSetupConfig() { }
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

        private static int executionNumber = 0;


        public Script Lua { get; private set; }
        public LuaScriptLoader LuaScriptLoader { get; private set; }

        public LuaGame Game { get; private set; }
        public LuaCsHook Hook { get; private set; }
        public LuaCsTimer Timer { get; private set; }
        public LuaCsNetworking Networking { get; private set; }
        public LuaCsSteam Steam { get; private set; }
        public LuaCsPerformanceCounter PerformanceCounter { get; private set; }

        public LuaCsModStore ModStore { get; private set; }
        private LuaRequire require { get; set; }

        public CsScriptLoader CsScriptLoader { get; private set; }
        public LuaCsSetupConfig Config { get; private set; }
        public MoonSharpVsCodeDebugServer DebugServer { get; private set; }

        private bool ShouldRunCs
        {
            get
            {
                return GetPackage(CsForBarotraumaId, false, true) != null || Config.ForceCsScripting;
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

            if (File.Exists(configFileName))
            {
                using (var file = File.Open(configFileName, FileMode.Open, FileAccess.Read))
                {
                    Config = LuaCsConfig.Load<LuaCsSetupConfig>(file);
                }
            }
            else
            {
                Config = new LuaCsSetupConfig();
            }
        }

        public static Type GetType(string typeName, bool throwOnError = false, bool ignoreCase = false)
        {
            if (typeName == null || typeName.Length == 0) { return null; }

            var byRef = false;
            if (typeName.StartsWith("out ") || typeName.StartsWith("ref "))
            {
                typeName = typeName.Remove(0, 4);
                byRef = true;
            }

            var type = Type.GetType(typeName, throwOnError, ignoreCase);
            if (type != null) { return byRef ? type.MakeByRefType() : type; }
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                // skip loaded assemblies that have revision that doesn't match the latest
                var assemblyName = a.GetName().Name!;
                if (CsScriptBase.Revision.TryGetValue(assemblyName, out var latestRev))
                {
                    var attrs = a.GetCustomAttributes<AssemblyMetadataAttribute>();
                    var revision = attrs.FirstOrDefault(attr => attr.Key == "Revision")?.Value;
                    if (revision != null && int.Parse(revision) != latestRev) { continue; }
                }

                type = a.GetType(typeName, throwOnError, ignoreCase);
                
                if (type != null)
                {
                    return byRef ? type.MakeByRefType() : type;
                }
            }
            return null;
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

        public void UpdateConfig()
        {
            FileStream file;
            if (!File.Exists(configFileName)) { file = File.Create(configFileName); }
            else { file = File.Open(configFileName, FileMode.Truncate, FileAccess.Write); }
            LuaCsConfig.Save(file, Config);
            file.Close();
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
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies().Where(a => CsScriptLoader?.LoadedAssemblies.Contains(a) ?? false).SelectMany(assembly => assembly.GetTypes()))
            {
                UserData.UnregisterType(type, true);
            }

            foreach (var mod in ACsMod.LoadedMods.ToArray())
            {
                mod.Dispose();
            }
            
            ACsMod.LoadedMods.Clear();

            if (Thread.CurrentThread == GameMain.MainThread) 
            {
                Hook?.Call("stop");
            }

            if (Lua != null && DebugServer.IsStarted)
            {
                DebugServer.Detach(Lua);
            }

            Game?.Stop();

            Hook.Clear();
            ModStore.Clear();
            Game = new LuaGame();
            Networking = new LuaCsNetworking();
            Timer = new LuaCsTimer();
            Steam = new LuaCsSteam();
            PerformanceCounter = new LuaCsPerformanceCounter();
            LuaScriptLoader = null;
            Lua = null;

            if (CsScriptLoader != null)
            {
                CsScriptLoader.Clear();
                CsScriptLoader.Unload();
                CsScriptLoader = null;
            }
        }

        public void Initialize(bool forceEnableCs = false)
        {
            Stop();

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

            UserData.RegisterExtensionType(typeof(MathUtils));
            UserData.RegisterExtensionType(typeof(XMLExtensions));

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
            Lua.Globals["LuaCsConfig"] = Config;

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

                if (Config.FirstTimeCsWarning)
                {
                    Config.FirstTimeCsWarning = false;
                    UpdateConfig();

                    DebugConsole.AddWarning("Cs package active! Cs mods are NOT sandboxed, use it at your own risk!");
                }

                CsScriptLoader = new CsScriptLoader();
                CsScriptLoader.SearchFolders();
                if (CsScriptLoader.HasSources)
                {
                    try
                    {
                        Stopwatch compilationTime = new Stopwatch();
                        compilationTime.Start();
                        var modTypes = CsScriptLoader.CompileAll();

                        modTypes.ForEach(t =>
                        {
                            t.GetConstructor(new Type[] { })?.Invoke(null);
                        });

                        compilationTime.Stop();
                        LuaCsLogger.LogMessage($"Took {compilationTime.ElapsedMilliseconds}ms to compile and run Cs Scripts.");
                    }
                    catch (Exception ex)
                    {
                        LuaCsLogger.HandleException(ex, LuaCsMessageOrigin.CSharpMod);
                    }
                }

            }


            ContentPackage luaPackage = GetPackage(LuaForBarotraumaId);

            void runLocal()
            {
                LuaCsLogger.LogMessage("Using LuaSetup.lua from the Barotrauma Lua/ folder.");
                string luaPath = LuaSetupFile;
                CallLuaFunction(Lua.LoadFile(luaPath), Path.GetDirectoryName(Path.GetFullPath(luaPath)));
            }

            void runWorkshop()
            {
                LuaCsLogger.LogMessage("Using LuaSetup.lua from the content package.");
                string luaPath = Path.Combine(Path.GetDirectoryName(luaPackage.Path), "Binary/Lua/LuaSetup.lua");
                CallLuaFunction(Lua.LoadFile(luaPath), Path.GetDirectoryName(Path.GetFullPath(luaPath)));
            }

            void runNone()
            {
                LuaCsLogger.LogError("LuaSetup.lua not found! Lua/LuaSetup.lua, no Lua scripts will be executed or work.", LuaCsMessageOrigin.LuaMod);
            }

            if (Config.PreferToUseWorkshopLuaSetup)
            {
                if (luaPackage != null) { runWorkshop(); }
                else if (File.Exists(LuaSetupFile)) { runLocal(); }
                else { runNone(); }
            }
            else
            {
                if (File.Exists(LuaSetupFile)) { runLocal(); }
                else if (luaPackage != null) { runWorkshop(); }
                else { runNone(); }
            }

            executionNumber++;
        }
    }
}
