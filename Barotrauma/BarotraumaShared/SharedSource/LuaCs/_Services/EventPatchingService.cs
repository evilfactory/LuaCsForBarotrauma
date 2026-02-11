using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using MonoMod.RuntimeDetour;
using static Barotrauma.ContentPackageManager;

namespace Barotrauma.LuaCs;


internal class EventPatchingService : IService
{
    public bool IsDisposed { get; private set; }
    private static EventPatchingService _instance;
    private IEventService _eventService;
    private ILoggerService _loggerService;
    /// <summary>
    /// Key: Original Method, Value: Active Hook.
    /// </summary>
    private readonly ConcurrentDictionary<MethodInfo, Hook> _runtimeHooks = new();

    #region METHODINFO
    
    private static readonly MethodInfo Screen_Select_Orig = 
        typeof(Screen).GetMethod(nameof(Screen.Select), BindingFlags.Instance | BindingFlags.Public);

    private static readonly MethodInfo EnabledPackages_SetCore_Orig =
        typeof(ContentPackageManager.EnabledPackages).GetMethod(nameof(ContentPackageManager.EnabledPackages.SetCore),
            BindingFlags.Static | BindingFlags.Public);
    
    private static readonly MethodInfo EnabledPackages_SetRegular_Orig =
        typeof(ContentPackageManager.EnabledPackages).GetMethod(nameof(ContentPackageManager.EnabledPackages.SetRegular),
            BindingFlags.Static | BindingFlags.Public);
    
    private static readonly MethodInfo Character_Create_Orig =
        AccessTools.DeclaredMethod(typeof(Character),
            nameof(Character.Create), new []
            {
                typeof(CharacterPrefab),
                typeof(Vector2), 
                typeof(string), 
                typeof(CharacterInfo), 
                typeof(ushort), 
                typeof(bool), 
                typeof(bool), 
                typeof(bool), 
                typeof(RagdollParams), 
                typeof(bool)
            });

    
    #endregion

    public EventPatchingService(IEventService eventService, ILoggerService loggerService)
    {
        _eventService = eventService;
        _loggerService = loggerService;
        _instance = this;
    }

    public void GenerateMethodHooks()
    {
        IService.CheckDisposed(this);
        
        if (!_runtimeHooks.IsEmpty)
        {
            _loggerService.LogError($"{nameof(GenerateMethodHooks)}: Hooks are already active!");
            return;
        }

        _runtimeHooks.TryAdd(Screen_Select_Orig, new Hook(
            Screen_Select_Orig,
            this.Screen_Select_Post
        ));

        _runtimeHooks.TryAdd(EnabledPackages_SetCore_Orig, new Hook(
            EnabledPackages_SetCore_Orig,
            typeof(EventPatchingService).GetMethod(nameof(EnabledPackages_SetCore_Post))
        ));
        
        _runtimeHooks.TryAdd(EnabledPackages_SetRegular_Orig, new Hook(
            EnabledPackages_SetRegular_Orig,
            typeof(EventPatchingService).GetMethod(nameof(EnabledPackages_SetRegular_Post))
        ));
        
        _runtimeHooks.TryAdd(Character_Create_Orig, new Hook(
            Character_Create_Orig,
            this.Character_Create_Post
        ));
    }

    public void CoroutineManager_Update_Post()
    {
        _eventService.PublishEvent<IEventUpdate>(x => x.OnUpdate(Timing.TotalTime));
        _loggerService.ProcessLogs();
    }
    
    public void Screen_Select_Post(Action<Screen> orig, Screen src)
    {
        orig(src);
        _eventService.PublishEvent<IEventScreenSelected>(x => x.OnScreenSelected(Screen.Selected));
    }

    public void PackageSource_Refresh_Post()
    {
        _eventService.PublishEvent<IEventAllPackageListChanged>(x => x.OnAllPackageListChanged(ContentPackageManager.CorePackages, ContentPackageManager.RegularPackages));
    }

    public void ContentPackageManager_Init_Post()
    {
        _eventService.PublishEvent<IEventAllPackageListChanged>(x => x.OnAllPackageListChanged(ContentPackageManager.CorePackages, ContentPackageManager.RegularPackages));
        _eventService.PublishEvent<IEventEnabledPackageListChanged>(sub => sub.OnEnabledPackageListChanged(EnabledPackages.Core, EnabledPackages.Regular));
    }

    public static void EnabledPackages_SetCore_Post(Action<CorePackage> orig, CorePackage newCore)
    {
        orig(newCore);
        _instance?._eventService.PublishEvent<IEventEnabledPackageListChanged>(sub => sub.OnEnabledPackageListChanged(EnabledPackages.Core, EnabledPackages.Regular));
    }

    public static void EnabledPackages_SetRegular_Post(Action<IReadOnlyList<RegularPackage>> orig, IReadOnlyList<RegularPackage> newRegular)
    {
        orig(newRegular);
        _instance?._eventService.PublishEvent<IEventEnabledPackageListChanged>(sub => sub.OnEnabledPackageListChanged(EnabledPackages.Core, EnabledPackages.Regular));
    }

#if CLIENT
    public void GameClient_ReadDataMessage_Pre(IReadMessage inc)
    {
        ServerPacketHeader header = (ServerPacketHeader)inc.ReadByte();
        _eventService.PublishEvent<IEventServerRawNetMessageReceived>(x => x.OnReceivedServerNetMessage(inc, header));
        inc.BitPosition -= 8; // rewind so the game can read the message
    }

    public void SubEditorScreen_Selected_Post(Screen __instance)
    {
        _eventService.PublishEvent<IEventScreenSelected>(x => x.OnScreenSelected(__instance));
    }

    public void PlayerInput_Update_Pre(double deltaTime)
    {
        _eventService.PublishEvent<IEventKeyUpdate>(x => x.OnKeyUpdate(deltaTime));
    }
#elif SERVER
    public void GameServer_ReadDataMessage_Pre(NetworkConnection sender, IReadMessage inc)
    {
        ClientPacketHeader header = (ClientPacketHeader)inc.ReadByte();
        _eventService.PublishEvent<IEventClientRawNetMessageReceived>(x => x.OnReceivedClientNetMessage(inc, header, sender));
        inc.BitPosition -= 8; // rewind so the game can read the message
    }
#endif
    
    // Character.Create(), Line 1411. 
    public Character Character_Create_Post(Func<CharacterPrefab, Vector2, string, CharacterInfo, ushort, bool, bool, bool, RagdollParams, bool, Character> orig, 
        CharacterPrefab prefab, Vector2 position, string seed, 
        CharacterInfo characterInfo = null, ushort id = Entity.NullEntityID, 
        bool isRemotePlayer = false, bool hasAi = true, bool createNetworkEvent = true, 
        RagdollParams ragdoll = null, bool spawnInitialItems = true)
    {
        Character result = orig(prefab, position, seed, characterInfo, id, isRemotePlayer, hasAi, createNetworkEvent,
            ragdoll, spawnInitialItems);
        _eventService.PublishEvent<IEventCharacterCreated>(x => x.OnCharacterCreated(result));
        return result;
    }

    public void Character_GiveJobItems_Post(Character __instance, WayPoint spawnPoint, bool isPvPMode)
    {
        _eventService.PublishEvent<IEventGiveCharacterJobItems>(x => x.OnGiveCharacterJobItems(__instance, spawnPoint, isPvPMode));
    }

    public void Affliction_Update_Post(Affliction __instance, CharacterHealth characterHealth, Limb targetLimb, float deltaTime)
    {
        _eventService.PublishEvent<IEventAfflictionUpdate>(x => x.OnAfflictionUpdate(__instance, characterHealth, targetLimb, deltaTime));
    }

    public void Dispose()
    {
        IsDisposed = true;
        foreach (var runtimeHook in _runtimeHooks)
        {
            runtimeHook.Value.Dispose();
        }
        _runtimeHooks.Clear();
    }
}
