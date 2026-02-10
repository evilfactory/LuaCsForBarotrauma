using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using static Barotrauma.ContentPackageManager;

namespace Barotrauma.LuaCs;

[HarmonyPatch]
internal class HarmonyEventPatchesService : IService
{
    public bool IsDisposed { get; private set; }

    private static IEventService _eventService;
    private static ILoggerService _loggerService;
    private readonly Harmony Harmony;

    public HarmonyEventPatchesService(IEventService eventService, ILoggerService loggerService)
    {
        _eventService = eventService;
        _loggerService = loggerService;
        Harmony = new Harmony("LuaCsForBarotrauma.Events");
        Harmony.PatchAll(typeof(HarmonyEventPatchesService));
    }

    [HarmonyPatch(typeof(CoroutineManager), nameof(CoroutineManager.Update)), HarmonyPostfix]
    public static void CoroutineManager_Update_Post()
    {
        _eventService.PublishEvent<IEventUpdate>(x => x.OnUpdate(Timing.TotalTime));
        _loggerService.ProcessLogs();
    }

    [HarmonyPatch(typeof(Screen), nameof(Screen.Select)), HarmonyPostfix]
    public static void Screen_Selected_Post(Screen __instance)
    {
        _eventService.PublishEvent<IEventScreenSelected>(x => x.OnScreenSelected(__instance));
    }

    [HarmonyPatch(typeof(ContentPackageManager.PackageSource), nameof(ContentPackageManager.PackageSource.Refresh)), HarmonyPostfix]
    public static void PackageSource_Refresh_Post()
    {
        _eventService.PublishEvent<IEventAllPackageListChanged>(x => x.OnAllPackageListChanged(ContentPackageManager.CorePackages, ContentPackageManager.RegularPackages));
    }

    [HarmonyPatch(typeof(ContentPackageManager), nameof(ContentPackageManager.Init)), HarmonyPostfix]
    public static void ContentPackageManager_Init_Post()
    {
        _eventService.PublishEvent<IEventAllPackageListChanged>(x => x.OnAllPackageListChanged(ContentPackageManager.CorePackages, ContentPackageManager.RegularPackages));
        _eventService.PublishEvent<IEventEnabledPackageListChanged>(sub => sub.OnEnabledPackageListChanged(EnabledPackages.Core, EnabledPackages.Regular));
    }

    [HarmonyPatch(typeof(ContentPackageManager.EnabledPackages), nameof(ContentPackageManager.EnabledPackages.SetCore)), HarmonyPostfix]
    public static void EnabledPackages_SetCore_Post()
    {
        _eventService.PublishEvent<IEventEnabledPackageListChanged>(sub => sub.OnEnabledPackageListChanged(EnabledPackages.Core, EnabledPackages.Regular));
    }

    [HarmonyPatch(typeof(ContentPackageManager.EnabledPackages), nameof(ContentPackageManager.EnabledPackages.SetRegular)), HarmonyPostfix]
    public static void EnabledPackages_SetRegular_Post()
    {
        _eventService.PublishEvent<IEventEnabledPackageListChanged>(sub => sub.OnEnabledPackageListChanged(EnabledPackages.Core, EnabledPackages.Regular));
    }



#if CLIENT
    [HarmonyPatch(typeof(GameClient), "ReadDataMessage"), HarmonyPrefix]
    public static void GameClient_ReadDataMessage_Pre(IReadMessage inc)
    {
        ServerPacketHeader header = (ServerPacketHeader)inc.PeekByte(); // Read but don't advance the read pointer
        _eventService.PublishEvent<IEventServerRawNetMessageReceived>(x => x.OnReceivedServerNetMessage(inc, header));
    }

    [HarmonyPatch(typeof(SubEditorScreen), nameof(SubEditorScreen.Select), new Type[] { }), HarmonyPostfix]
    public static void SubEditorScreen_Selected_Post(Screen __instance)
    {
        _eventService.PublishEvent<IEventScreenSelected>(x => x.OnScreenSelected(__instance));
    }

    [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.Update)), HarmonyPrefix]
    public static void PlayerInput_Update_Pre(double deltaTime)
    {
        _eventService.PublishEvent<IEventKeyUpdate>(x => x.OnKeyUpdate(deltaTime));
    }
#elif SERVER
    [HarmonyPatch(typeof(GameServer), "ReadDataMessage"), HarmonyPrefix]
    public static void GameServer_ReadDataMessage_Pre(NetworkConnection sender, IReadMessage inc)
    {
        ClientPacketHeader header = (ClientPacketHeader)inc.PeekByte(); // Read but don't advance the read pointer
        _eventService.PublishEvent<IEventClientRawNetMessageReceived>(x => x.OnReceivedClientNetMessage(inc, header, sender));
    }
#endif

    [HarmonyPatch(typeof(Character), nameof(Character.Create), new[] { 
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
    }), HarmonyPostfix]
    public static void Character_Create_Post(Character __result)
    {
        _eventService.PublishEvent<IEventCharacterCreated>(x => x.OnCharacterCreated(__result));
    }

    [HarmonyPatch(typeof(Character), nameof(Character.GiveJobItems)), HarmonyPostfix]
    public static void Character_GiveJobItems_Post(Character __instance, WayPoint spawnPoint, bool isPvPMode)
    {
        _eventService.PublishEvent<IEventGiveCharacterJobItems>(x => x.OnGiveCharacterJobItems(__instance, spawnPoint, isPvPMode));
    }

    [HarmonyPatch(typeof(Affliction), nameof(Affliction.Update)), HarmonyPostfix]
    public static void Affliction_Update_Post(Affliction __instance, CharacterHealth characterHealth, Limb targetLimb, float deltaTime)
    {
        _eventService.PublishEvent<IEventAfflictionUpdate>(x => x.OnAfflictionUpdate(__instance, characterHealth, targetLimb, deltaTime));
    }

    public void Dispose()
    {
        Harmony.UnpatchSelf();
        IsDisposed = true;
    }
}
