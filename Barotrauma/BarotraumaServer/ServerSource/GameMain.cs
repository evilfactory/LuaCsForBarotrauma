﻿using Barotrauma.Networking;
using Barotrauma.Steam;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Barotrauma.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using MoonSharp.Interpreter;
using System.Net;
using Barotrauma.Extensions;

namespace Barotrauma
{
    class GameMain
    {
        public static readonly Version Version = Assembly.GetEntryAssembly().GetName().Version;

        public static bool IsSingleplayer => NetworkMember == null;
        public static bool IsMultiplayer => NetworkMember != null;

        private static World world;
        public static World World
        {
            get
            {
                if (world == null) { world = new World(new Vector2(0, -9.82f)); }
                return world;
            }
            set { world = value; }
        }

        public static LuaCsSetup LuaCs;

        public static GameServer Server;
        public static NetworkMember NetworkMember
        {
            get { return Server; }
        }

        public static GameSession GameSession;

        public static GameMain Instance
        {
            get;
            private set;
        }

        public static Thread MainThread { get; private set; }

        //only screens the server implements
        public static GameScreen GameScreen;
        public static NetLobbyScreen NetLobbyScreen;

        //null screens because they are not implemented by the server,
        //but they're checked for all over the place
        //TODO: maybe clean up instead of having these constants
        public static readonly Screen SubEditorScreen = UnimplementedScreen.Instance;

        public static bool ShouldRun = true;

        private static Stopwatch stopwatch;

        private static readonly Queue<int> prevUpdateRates = new Queue<int>();
        private static int updateCount = 0;
        
        public static ContentPackage VanillaContent => ContentPackageManager.VanillaCorePackage;


        public readonly string[] CommandLineArgs;

        public GameMain(string[] args)
        {
            Instance = this;

            CommandLineArgs = args;

            World = new World(new Vector2(0, -9.82f));
            FarseerPhysics.Settings.AllowSleep = true;
            FarseerPhysics.Settings.ContinuousPhysics = false;
            FarseerPhysics.Settings.VelocityIterations = 1;
            FarseerPhysics.Settings.PositionIterations = 1;

            Console.WriteLine("Loading game settings");
            GameSettings.Init();

            //no owner key = dedicated server
            if (!CommandLineArgs.Any(a => a.Trim().Equals("-ownerkey", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("Initializing SteamManager");
                SteamManager.Initialize();

                if (!SteamManager.SteamworksLibExists)
                {
                    Console.WriteLine("Initializing EosManager");
                    if (EosInterface.Core.Init(EosInterface.ApplicationCredentials.Server, enableOverlay: false).TryUnwrapFailure(out var initError))
                    {
                        Console.WriteLine($"EOS failed to initialize: {initError}");
                    }
                }
            }

            //TODO: figure out how consent is supposed to work for servers
            //Console.WriteLine("Initializing GameAnalytics");
            //GameAnalyticsManager.InitIfConsented();

            Console.WriteLine("Initializing GameScreen");
            GameScreen = new GameScreen();

            MainThread = Thread.CurrentThread;
        }

        public void Init()
        {
            CoreEntityPrefab.InitCorePrefabs();

            GameModePreset.Init();

            ContentPackageManager.Init().Consume();
            ContentPackageManager.LogEnabledRegularPackageErrors();

            SubmarineInfo.RefreshSavedSubs();

            Screen.SelectNull();

            NetLobbyScreen = new NetLobbyScreen();

            CheckContentPackage();

            LuaCs = new LuaCsSetup();
        }


        private void CheckContentPackage()
        {
            if (Version < VanillaContent.GameVersion)
            {
                DebugConsole.ThrowErrorLocalized(
                    TextManager.GetWithVariables("versionmismatchwarning",
                        ("[gameversion]", Version.ToString()),
                        ("[contentversion]", VanillaContent.GameVersion.ToString())));
            }
        }

        public void StartServer()
        {
            string name = "Server";
            int port = NetConfig.DefaultPort;
            int queryPort = NetConfig.DefaultQueryPort;
            bool publiclyVisible = false;
            string password = "";
            bool enableUpnp = false;

            int maxPlayers = 10; 
            Option<int> ownerKey = Option.None;
            Option<P2PEndpoint> ownerEndpoint = Option.None;
            IPAddress listenIp = IPAddress.Any;

            XDocument doc = XMLExtensions.TryLoadXml(ServerSettings.SettingsFile);
            if (doc?.Root == null)
            {
                DebugConsole.AddWarning("File \"" + ServerSettings.SettingsFile + "\" not found. Starting the server with default settings.");
            }
            else
            {
                name = doc.Root.GetAttributeString(nameof(ServerSettings.ServerName),
                    //backwards compatibility
                    doc.Root.GetAttributeString("name", "Server"));
                port = doc.Root.GetAttributeInt(nameof(ServerSettings.Port), NetConfig.DefaultPort);
                queryPort = doc.Root.GetAttributeInt(nameof(ServerSettings.QueryPort), NetConfig.DefaultQueryPort);
                publiclyVisible = doc.Root.GetAttributeBool(nameof(ServerSettings.IsPublic), false);
                enableUpnp = doc.Root.GetAttributeBool(nameof(ServerSettings.EnableUPnP), false);
                maxPlayers = doc.Root.GetAttributeInt(nameof(ServerSettings.MaxPlayers), 10);
                password = doc.Root.GetAttributeString("password", "");
                ownerKey = Option<int>.None();
            }
            
#if DEBUG
            foreach (string s in CommandLineArgs)
            {
                Console.WriteLine(s);
            }
#endif

            for (int i = 0; i < CommandLineArgs.Length; i++)
            {
                switch (CommandLineArgs[i].Trim().ToLowerInvariant())
                {
                    case "-name":
                        name = CommandLineArgs[i + 1];
                        i++;
                        break;
                    case "-ip":
                        if (IPAddress.TryParse(CommandLineArgs[i + 1], out IPAddress address))
                            listenIp = address;
                        else
                            DebugConsole.ThrowError($"Invalid Ip Address '{CommandLineArgs[i + 1]}'.");
                        break;
                    case "-port":
                        int.TryParse(CommandLineArgs[i + 1], out port);
                        i++;
                        break;
                    case "-queryport":
                        int.TryParse(CommandLineArgs[i + 1], out queryPort);
                        i++;
                        break;
                    case "-public":
                        bool.TryParse(CommandLineArgs[i + 1], out publiclyVisible);
                        i++;
                        break;
                    case "-password":
                        password = CommandLineArgs[i + 1];
                        i++;
                        break;
                    case "-nopassword":
                        password = "";
                        break;
                    case "-upnp":
                    case "-enableupnp":
                        bool.TryParse(CommandLineArgs[i + 1], out enableUpnp);
                        i++;
                        break;
                    case "-maxplayers":
                        int.TryParse(CommandLineArgs[i + 1], out maxPlayers);
                        i++;
                        break;
                    case "-ownerkey":
                        if (int.TryParse(CommandLineArgs[i + 1], out int key))
                        {
                            ownerKey = Option<int>.Some(key);
                        }
                        i++;
                        break;
                    case "-endpoint":
                        ownerEndpoint = P2PEndpoint.Parse(CommandLineArgs[i + 1]);
                        i++;
                        break;
                    case "-pipes":
                        //handled in TryStartChildServerRelay
                        i += 2;
                        break;
                }
            }

            Server = new GameServer(
                name,
                listenIp,
                port,
                queryPort,
                publiclyVisible,
                password,
                enableUpnp,
                maxPlayers,
                ownerKey,
                ownerEndpoint);
            Server.StartServer(registerToServerList: true);

            for (int i = 0; i < CommandLineArgs.Length; i++)
            {
                switch (CommandLineArgs[i].Trim().ToLowerInvariant())
                {
                    case "-playstyle":
                        Enum.TryParse(CommandLineArgs[i + 1], out PlayStyle playStyle);
                        Server.ServerSettings.PlayStyle = playStyle;
                        i++;
                        break;
                    case "-banafterwrongpassword":
                        bool.TryParse(CommandLineArgs[i + 1], out bool banAfterWrongPassword);
                        Server.ServerSettings.BanAfterWrongPassword = banAfterWrongPassword;
                        break;
                    case "-karma":
                    case "-karmaenabled":
                        bool.TryParse(CommandLineArgs[i + 1], out bool karmaEnabled);
                        Server.ServerSettings.KarmaEnabled = karmaEnabled;
                        i++;
                        break;
                    case "-karmapreset":
                        string karmaPresetName = CommandLineArgs[i + 1];
                        Server.ServerSettings.KarmaPreset = karmaPresetName;
                        i++;
                        break;
                    case "-language":
                        LanguageIdentifier language = CommandLineArgs[i + 1].ToLanguageIdentifier();
                        if (ServerLanguageOptions.Options.Any(o => o.Identifier == language))
                        {
                            Server.ServerSettings.Language = language;
                        }
                        i++;
                        break;
                    case "-multiclienttestmode":
#if DEBUG
                        CharacterCampaignData.RequireClientNameMatch = true;
#endif
                        i++;
                        break;
                }
            }
        }

        public void CloseServer()
        {
            Server?.Quit();
            ShouldRun = false;
            Server = null;
        }

        public void Run()
        {
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Character));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Item));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Items.Components.ItemComponent));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Hull));

            Init();
            StartServer();

            ResetFrameTime();

            double frequency = (double)Stopwatch.Frequency;
            if (frequency <= 1500)
            {
                DebugConsole.NewMessage("WARNING: Stopwatch frequency under 1500 ticks per second. Expect significant syncing accuracy issues.", Color.Yellow);
            }

            Stopwatch performanceCounterTimer = Stopwatch.StartNew();

            stopwatch = Stopwatch.StartNew();
            long prevTicks = stopwatch.ElapsedTicks;
            while (ShouldRun)
            {
                long currTicks = stopwatch.ElapsedTicks;
                double elapsedTime = Math.Max(currTicks - prevTicks, 0) / frequency;
                Timing.Accumulator += elapsedTime;
                if (Timing.Accumulator > Timing.AccumulatorMax)
                {
                    //prevent spiral of death:
                    //if the game's running too slowly then we have no choice but to skip a bunch of steps
                    //otherwise it snowballs and becomes unplayable
                    Timing.Accumulator = Timing.Step;
                }
                
                CrossThread.ProcessTasks();
                
                prevTicks = currTicks;
                while (Timing.Accumulator >= Timing.Step)
                {
                    performanceCounterTimer.Start();

                    Timing.TotalTime += Timing.Step;
                    Timing.TotalTimeUnpaused += Timing.Step;                    
                    DebugConsole.Update();
                    if (GameSession?.GameMode == null || !GameSession.GameMode.Paused)
                    {
                        Screen.Selected?.Update((float)Timing.Step);
                    }
                    Server.Update((float)Timing.Step);
                    if (Server == null) { break; }
                    SteamManager.Update((float)Timing.Step);
                    EosInterface.Core.Update();
                    TaskPool.Update();
                    CoroutineManager.Update(paused: false, (float)Timing.Step);

                    GameMain.LuaCs.Update();
                    performanceCounterTimer.Stop();
                    if (GameMain.LuaCs.PerformanceCounter.EnablePerformanceCounter)
                    {
                        GameMain.LuaCs.PerformanceCounter.UpdateElapsedTime = (double)performanceCounterTimer.ElapsedTicks / Stopwatch.Frequency;
                    }
                    performanceCounterTimer.Reset();

                    Timing.Accumulator -= Timing.Step;
                    updateCount++;
                }

#if !DEBUG
                if (Server?.OwnerConnection == null)
                {
                    DebugConsole.UpdateCommandLine((int)(Timing.Accumulator * 800));
                }
                else
                {
                    DebugConsole.Clear();
                }
#else
                DebugConsole.UpdateCommandLine((int)(Timing.Accumulator * 800));
#endif

                int frameTime = (int)((stopwatch.ElapsedTicks - prevTicks) / frequency * 1000.0);
                frameTime = Math.Max(0, frameTime);
                
                Thread.Sleep(Math.Max(((int)(Timing.Step * 1000.0) - frameTime) / 2, 0));

                if (performanceCounterTimer.ElapsedMilliseconds > 1000)
                {
                    int updateRate = (int)Math.Round(updateCount / (double)(performanceCounterTimer.ElapsedMilliseconds / 1000.0));
                    prevUpdateRates.Enqueue(updateRate);
                    if (prevUpdateRates.Count >= 10)
                    {
                        int avgUpdateRate = (int)prevUpdateRates.Average();
                        if (avgUpdateRate < Timing.FixedUpdateRate * 0.98 && GameSession != null && GameSession.RoundDuration > 1.0)
                        {
                            DebugConsole.AddWarning($"Running slowly ({avgUpdateRate} updates/s)!");
                            if (Server != null)
                            {
                                foreach (Client c in Server.ConnectedClients)
                                {
                                    if (c.Connection == Server.OwnerConnection || c.Permissions != ClientPermissions.None)
                                    {
                                        Server.SendConsoleMessage($"Server running slowly ({avgUpdateRate} updates/s)!", c, Color.Orange);
                                    }
                                }
                            }
                        }
                        prevUpdateRates.Clear();
                    }
                    performanceCounterTimer.Restart();
                    updateCount = 0;
                }
            }
            stopwatch.Stop();

            CloseServer();

            SteamManager.ShutDown();

            SaveUtil.CleanUnnecessarySaveFiles();

            if (GameSettings.CurrentConfig.SaveDebugConsoleLogs
                || GameSettings.CurrentConfig.VerboseLogging) { DebugConsole.SaveLogs(); }
            if (GameAnalyticsManager.SendUserStatistics) { GameAnalyticsManager.ShutDown(); }

            MainThread = null;
        }

        public static void ResetFrameTime()
        {
            Timing.Accumulator = 0.0f;
            stopwatch?.Restart();
            prevUpdateRates.Clear();
            updateCount = 0;
        }
        
        public CoroutineHandle ShowLoading(IEnumerable<CoroutineStatus> loader, bool waitKeyHit = true)
        {
            return CoroutineManager.StartCoroutine(loader);
        }

        public void Exit()
        {
            ShouldRun = false;
            GameMain.LuaCs.Stop();
        }
    }
}
