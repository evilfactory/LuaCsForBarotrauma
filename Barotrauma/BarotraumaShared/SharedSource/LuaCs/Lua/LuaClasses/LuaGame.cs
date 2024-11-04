using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;

namespace Barotrauma
{
    partial class LuaGame
    {
        public bool IsSingleplayer => GameMain.IsSingleplayer;
        public bool IsMultiplayer => GameMain.IsMultiplayer;
        public string SaveFolder => string.IsNullOrEmpty(GameSettings.CurrentConfig.SavePath) ? SaveUtil.DefaultSaveFolder : GameSettings.CurrentConfig.SavePath;

#if CLIENT
        public GameClient Client
        {
            get
            {
                return GameMain.Client;
            }
        }

        public bool Paused => GameMain.Instance?.Paused == true;
        public byte SessionId => GameMain.Client.SessionId;
        public byte MyID => SessionId; // compatibility

        public ChatMode ActiveChatMode => GameMain.ActiveChatMode;

        public ChatBox ChatBox
        {
            get
            {
                if (GameMain.IsSingleplayer)
                    return GameMain.GameSession.CrewManager.ChatBox;
                else
                    return GameMain.Client.ChatBox;
            }
        }

        public Sounds.SoundManager SoundManager
        {
            get
            {
                return GameMain.SoundManager;
            }
        }

        public Lights.LightManager LightManager
        {
            get
            {
                return GameMain.LightManager;
            }
        }

        public SubEditorScreen SubEditorScreen
        {
            get
            {
                return GameMain.SubEditorScreen;
            }
        }

        public MainMenuScreen MainMenuScreen
        {
            get
            {
                return GameMain.MainMenuScreen;
            }
        }

        public Particles.ParticleManager ParticleManager
        {
            get
            {
                return GameMain.ParticleManager;
            }
        }

        public bool IsSubEditor
        {
            get
            {
                return Screen.Selected is SubEditorScreen;
            }
        }

#else
        public GameServer Server
        {
            get
            {
                return GameMain.Server;
            }
        }

        public bool IsDedicated
        {
            get
            {
                return GameMain.Server.ServerPeer is LidgrenServerPeer;
            }
        }

        public bool Paused => false;
#endif

        public ServerSettings ServerSettings
        {
            get
            {
#if SERVER
                return GameMain.Server.ServerSettings;
#else
                return GameMain.Client.ServerSettings;
#endif
            }
        }
        
        public RespawnManager RespawnManager
        {
            get
            {
#if SERVER
                return GameMain.Server.RespawnManager;
#else
                return GameMain.Client.RespawnManager;
#endif
            }
        }

        public bool? ForceVoice = null;
        public bool? ForceLocalVoice = null;

        public DynValue Settings;

        public bool allowWifiChat = false;
        public bool overrideTraitors = false;
        public bool overrideRespawnSub = false;
        public bool overrideSignalRadio = false;
        public bool disableSpamFilter = false;
        public bool disableDisconnectCharacter = false;
        public bool enableControlHusk = false;

        public int MapEntityUpdateInterval
        {
            get { return MapEntity.MapEntityUpdateInterval; }
            set { MapEntity.MapEntityUpdateInterval = value; }
        }

        public int GapUpdateInterval
        {
            get { return 1; }
            set { }
        }

        public int PoweredUpdateInterval
        {
            get { return MapEntity.PoweredUpdateInterval; }
            set { MapEntity.PoweredUpdateInterval = value; }
        }

        public int CharacterUpdateInterval
        {
            get { return Character.CharacterUpdateInterval; }
            set { Character.CharacterUpdateInterval = value; }
        }


        public HashSet<Item> UpdatePriorityItems = new HashSet<Item>();
        public HashSet<Character> UpdatePriorityCharacters = new HashSet<Character>();

        public void AddPriorityItem(Item item)
        {
            UpdatePriorityItems.Add(item);
        }

        public void RemovePriorityItem(Item item)
        {
            UpdatePriorityItems.Remove(item);
        }

        public void ClearPriorityItem()
        {
            UpdatePriorityItems.Clear();
        }

        public void AddPriorityCharacter(Character character)
        {
            UpdatePriorityCharacters.Add(character);
        }

        public void RemovePriorityCharacter(Character character)
        {
            UpdatePriorityCharacters.Remove(character);
        }

        public void ClearPriorityCharacter()
        {
            UpdatePriorityCharacters.Clear();
        }

        public bool RoundStarted
        {

            get
            {
                if (GameMain.IsSingleplayer) { return GameMain.GameSession != null && GameMain.GameSession.IsRunning; }
#if SERVER
                return GameMain.Server?.GameStarted == true;
#else
                return GameMain.Client?.GameStarted == true;
#endif
            }
        }

        public GameSession GameSession
        {
            get
            {
                return GameMain.GameSession;
            }
        }

        public NetLobbyScreen NetLobbyScreen
        {
            get
            {
                return GameMain.NetLobbyScreen;
            }
        }

        public GameScreen GameScreen
        {
            get
            {
                return GameMain.GameScreen;
            }
        }

        public World World
        {
            get
            {
                return GameMain.World;
            }
        }

#if SERVER
        public ServerPeer Peer
        {
            get
            {
                return GameMain.Server.ServerPeer;
            }
        }
#else
        public ClientPeer Peer
        {
            get
            {
                return GameMain.Client.ClientPeer;
            }
        }
#endif

        public LuaGame()
        {
            LuaUserData.MakeFieldAccessible(UserData.RegisterType(typeof(GameSettings)), "currentConfig");
            Settings = UserData.CreateStatic(typeof(GameSettings));
        }

        public void OverrideTraitors(bool o)
        {
            overrideTraitors = o;
        }

        public void OverrideRespawnSub(bool o)
        {
            overrideRespawnSub = o;
        }

        public void AllowWifiChat(bool o)
        {
            allowWifiChat = o;
        }

        public void OverrideSignalRadio(bool o)
        {
            overrideSignalRadio = o;
        }

        public void DisableSpamFilter(bool o)
        {
            disableSpamFilter = o;
        }

        public void DisableDisconnectCharacter(bool o)
        {
            disableDisconnectCharacter = o;
        }


        public void EnableControlHusk(bool o)
        {
            enableControlHusk = o;
        }

        public static void Explode(Vector2 pos, float range = 100, float force = 30, float damage = 30, float structureDamage = 30, float itemDamage = 30, float empStrength = 0, float ballastFloraStrength = 0)
        {
            new Explosion(range, force, damage, structureDamage, itemDamage, empStrength, ballastFloraStrength).Explode(pos, null);
        }

        public static string SpawnItem(string name, Vector2 pos, bool inventory = false, Character character = null)
        {
            string error;
            DebugConsole.SpawnItem(new string[] { name, inventory ? "inventory" : "cursor" }, pos, character, out error);
            return error;
        }

        public static ContentPackage[] GetEnabledContentPackages()
        {
            return ContentPackageManager.EnabledPackages.All.ToArray();
        }

        public static ItemPrefab GetItemPrefab(string itemNameOrId)
        {
            ItemPrefab itemPrefab =
            (MapEntityPrefab.Find(itemNameOrId, identifier: null, showErrorMessages: false) ??
            MapEntityPrefab.Find(null, identifier: itemNameOrId, showErrorMessages: false)) as ItemPrefab;

            return itemPrefab;
        }

        public static Submarine GetRespawnSub()
        {
#if SERVER
            if (GameMain.Server.RespawnManager == null) { return null; }
            return GameMain.Server.RespawnManager.GetShuttle(CharacterTeamType.Team1);
#else
            if (GameMain.Client.RespawnManager == null) { return null; }
            return GameMain.Client.RespawnManager.GetShuttle(CharacterTeamType.Team1);
#endif
        }

        public static Items.Components.Steering GetSubmarineSteering(Submarine sub)
        {
            foreach (Item item in Item.ItemList)
            {
                if (item.Submarine != sub) continue;

                var steering = item.GetComponent<Items.Components.Steering>();
                if (steering != null)
                {
                    return steering;
                }
            }

            return null;
        }

        public static WifiComponent GetWifiComponent(Item item)
        {
            if (item == null) return null;
            return item.GetComponent<WifiComponent>();
        }

        public static LightComponent GetLightComponent(Item item)
        {
            if (item == null) return null;
            return item.GetComponent<LightComponent>();
        }

        public static CustomInterface GetCustomInterface(Item item)
        {
            if (item == null) return null;
            return item.GetComponent<CustomInterface>();
        }

        public static Fabricator GetFabricatorComponent(Item item)
        {
            if (item == null) return null;
            return item.GetComponent<Fabricator>();
        }

        public static Holdable GetHoldableComponent(Item item)
        {
            if (item == null) return null;
            return item.GetComponent<Holdable>();
        }

        public static void ExecuteCommand(string command)
        {
            DebugConsole.ExecuteCommand(command);
        }

        public static Signal CreateSignal(string value, int stepsTaken = 1, Character sender = null, Item source = null, float power = 0, float strength = 1)
        {
            return new Signal(value, stepsTaken, sender, source, power, strength);
        }

        private List<DebugConsole.Command> luaAddedCommand = new List<DebugConsole.Command>();
        public IEnumerable<DebugConsole.Command> LuaAddedCommand { get { return luaAddedCommand; } }

        public bool IsCustomCommandPermitted(Identifier command)
        {
            DebugConsole.Command[] permitted = new DebugConsole.Command[] 
            { 
                DebugConsole.FindCommand("cl_reloadluacs"),
                DebugConsole.FindCommand("cl_lua"),
                DebugConsole.FindCommand("cl_toggleluadebug"),
            };

            foreach (var consoleCommand in LuaAddedCommand.Concat(permitted.AsEnumerable())) 
            { 
                if (consoleCommand.Names.Contains(command))
                {
                    return true;
                }
            }

            return false;
        }

        public void RemoveCommand(string name)
        {
            for (var i = 0; i < DebugConsole.Commands.Count; i++)
            {
                foreach (var cmdname in DebugConsole.Commands[i].Names)
                {
                    if (cmdname == name)
                    {
                        luaAddedCommand.Remove(DebugConsole.Commands[i]);
                        DebugConsole.Commands.RemoveAt(i);
                        continue;
                    }
                }
            }
        }

        public void AddCommand(string name, string help, LuaCsAction onExecute, LuaCsFunc getValidArgs = null, bool isCheat = false)
        {
            var cmd = new DebugConsole.Command(name, help, (string[] arg1) => onExecute(new object[] { arg1 }),
                () =>
                {
                    if (getValidArgs == null) return null;
                    var validArgs = getValidArgs();
                    if (validArgs is DynValue luaValue)
                    {
                        return luaValue.ToObject<string[][]>();
                    }
                    return (string[][])validArgs;
                }, isCheat);

            luaAddedCommand.Add(cmd);
            DebugConsole.Commands.Add(cmd);
        }

        public List<DebugConsole.Command> Commands => DebugConsole.Commands;

        public void AssignOnExecute(string names, object onExecute) => DebugConsole.AssignOnExecute(names,
            (string[] a) =>
            {
                //GameMain.LuaCs.CallLuaFunction(onExecute, new object[] { a });
                throw new NotImplementedException();
            });

        public void SaveGame(string path)
        {
            if (!LuaCsFile.CanWriteToPath(path)) { throw new ScriptRuntimeException($"Saving files to {path} is disallowed."); }
            SaveUtil.SaveGame(CampaignDataPath.CreateRegular(path));
        }

        public void LoadGame(string path)
        {
            SaveUtil.LoadGame(CampaignDataPath.CreateRegular(path));
        }

#if SERVER
        public void LoadCampaign(string path, Client client = null)
        {
            MultiPlayerCampaign.LoadCampaign(CampaignDataPath.CreateRegular(path), client);
        }

        public static void SendMessage(string msg, ChatMessageType? messageType = null, Client sender = null, Character character = null)
        {
            GameMain.Server.SendChatMessage(msg, messageType, sender, character);
        }

        public static void SendTraitorMessage(WriteOnlyMessage message, Client client)
        {
            GameMain.Server.SendTraitorMessage(message, client);
        }

        public static void SendDirectChatMessage(string sendername, string text, Character sender, ChatMessageType messageType = ChatMessageType.Private, Client client = null, string iconStyle = "")
        {
            ChatMessage cm = ChatMessage.Create(sendername, text, messageType, sender);
            cm.IconStyle = iconStyle;
            GameMain.Server.SendDirectChatMessage(cm, client);
        }

        public static void SendDirectChatMessage(ChatMessage chatMessage, Client client)
        {
            GameMain.Server.SendDirectChatMessage(chatMessage, client);
        }

        public static void Log(string message, ServerLog.MessageType type)
        {
            GameServer.Log(message, type);
        }

        public static void DispatchRespawnSub()
        {
            GameMain.Server.RespawnManager.DispatchShuttle(GameMain.Server.RespawnManager.GetTeamSpecificState(CharacterTeamType.Team1));
        }

        public static GameServer.TryStartGameResult StartGame()
        {
            return GameMain.Server.TryStartGame();
        }

        public static void EndGame()
        {
            GameMain.Server.EndGame();
        }

        public void AssignOnClientRequestExecute(string names, object onExecute) => DebugConsole.AssignOnClientRequestExecute(names, (Client a, Vector2 b, string[] c) => { GameMain.LuaCs.CallLuaFunction(onExecute, new object[] { a, b, c }); });

#endif

        public void Stop()
        {
            MapEntityUpdateInterval = 1;
            CharacterUpdateInterval = 1;

            foreach (var cmd in luaAddedCommand)
            {
                DebugConsole.Commands.Remove(cmd);
            }
        }
    }

}
