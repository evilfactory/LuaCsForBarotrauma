using Barotrauma.Items.Components;
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
#if SERVER
        Harmony.PatchAll(typeof(HarmonyEventPatchesService.Patch_StartGame_End));
#endif
    }

    [HarmonyPatch(typeof(CoroutineManager), nameof(CoroutineManager.Update)), HarmonyPostfix]
    public static void CoroutineManager_Update_Post()
    {
        _eventService.PublishEvent<IEventUpdate>(x => x.OnUpdate(Timing.TotalTime));
        _loggerService.ProcessLogs();
    }

#if CLIENT
    [HarmonyPatch(typeof(GameSession), nameof(GameSession.StartRound), new Type[]
    {
        typeof(LevelData), typeof(bool), typeof(SubmarineInfo), typeof(SubmarineInfo)
    }), HarmonyPostfix]
    public static void GameSession_StartRound_Post()
    {
        _eventService.PublishEvent<IEventRoundStarted>(x => x.OnRoundStart());
    }
#endif

    [HarmonyPatch(typeof(GameSession), nameof(GameSession.EndRound)), HarmonyPrefix]
    public static void GameSession_EndRound_Pre()
    {
        _eventService.PublishEvent<IEventRoundEnded>(x => x.OnRoundEnd());
    }

    [HarmonyPatch(typeof(GameSession), nameof(GameSession.LoadPreviousSave)), HarmonyPrefix]
    public static void GameSession_LoadPreviousSave_Pre()
    {
        _eventService.PublishEvent<IEventRoundEnded>(x => x.OnRoundEnd());
    }

    [HarmonyPatch(typeof(GameSession), nameof(GameSession.EndMissions)), HarmonyPostfix]
    public static void GameSession_EndMission_Post(GameSession __instance)
    {
        _eventService.PublishEvent<IEventMissionsEnded>(x => x.OnMissionsEnded(__instance.Missions.ToList()));
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
        ServerPacketHeader header = (ServerPacketHeader)inc.ReadByte();
        _eventService.PublishEvent<IEventServerRawNetMessageReceived>(x => x.OnReceivedServerNetMessage(inc, header));
        inc.BitPosition -= 8; // rewind so the game can read the message
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
        ClientPacketHeader header = (ClientPacketHeader)inc.ReadByte();
        _eventService.PublishEvent<IEventClientRawNetMessageReceived>(x => x.OnReceivedClientNetMessage(inc, header, sender));
        inc.BitPosition -= 8; // rewind so the game can read the message
    }

    [HarmonyPatch(typeof(GameServer), "OnInitializationComplete"), HarmonyPostfix]
    public static void GameServer_OnInitializationComplete_Post(GameServer __instance)
    {
        Client client = __instance.ConnectedClients.LastOrDefault();
        if (client == null) { return; }
        _eventService.PublishEvent<IEventClientConnected>(x => x.OnClientConnected(client));
    }

    [HarmonyPatch(typeof(GameServer), nameof(GameServer.DisconnectClient), new Type[] { typeof(Client), typeof(PeerDisconnectPacket) }), HarmonyPrefix]
    public static void GameServer_DisconnectClient_Pre(Client client, PeerDisconnectPacket peerDisconnectPacket)
    {
        if (client == null) { return; }

        _eventService.PublishEvent<IEventClientDisconnected>(x => x.OnClientDisconnected(client));
    }

    [HarmonyPatch(typeof(GameServer), nameof(GameServer.AssignJobs)), HarmonyPostfix]
    public static void GameServer_AssignJobs_Post(List<Client> unassigned)
    {
        _eventService.PublishEvent<IEventJobsAssigned>(x => x.OnJobsAssigned(unassigned));
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

    [HarmonyPatch(typeof(Character), nameof(Character.Kill)), HarmonyPostfix]
    public static void Character_Kill_Post(Character __instance, Affliction causeOfDeathAffliction, CauseOfDeathType causeOfDeath)
    {
        _eventService.PublishEvent<IEventCharacterDeath>(x => x.OnCharacterDeath(__instance, causeOfDeathAffliction, causeOfDeath));
    }

    [HarmonyPatch(typeof(Character), nameof(Character.GiveJobItems)), HarmonyPostfix]
    public static void Character_GiveJobItems_Post(Character __instance, WayPoint spawnPoint, bool isPvPMode)
    {
        _eventService.PublishEvent<IEventGiveCharacterJobItems>(x => x.OnGiveCharacterJobItems(__instance, spawnPoint, isPvPMode));
    }

    [HarmonyPatch(typeof(Character), nameof(Character.DamageLimb)), HarmonyPrefix]
    public static bool Character_DamageLimb_Pre(AttackResult __result, Character __instance, Vector2 worldPosition, Limb hitLimb, IEnumerable<Affliction> afflictions, float stun, bool playSound, Vector2 attackImpulse, Character attacker, float damageMultiplier, bool allowStacking, float penetration, bool shouldImplode, bool ignoreDamageOverlay, bool recalculateVitality)
    {
        AttackResult? result = null;
        _eventService.PublishEvent<IEventCharacterDamageLimb>(x => result = x.OnCharacterDamageLimb(__instance, worldPosition, hitLimb, afflictions, stun, playSound, attackImpulse, attacker, damageMultiplier, allowStacking, penetration, shouldImplode));
        if (result != null)
        {
            __result = (AttackResult)result;
            return false; // skip
        }

        return true;
    }

    [HarmonyPatch(typeof(Affliction), nameof(Affliction.Update)), HarmonyPostfix]
    public static void Affliction_Update_Post(Affliction __instance, CharacterHealth characterHealth, Limb targetLimb, float deltaTime)
    {
        _eventService.PublishEvent<IEventAfflictionUpdate>(x => x.OnAfflictionUpdate(__instance, characterHealth, targetLimb, deltaTime));
    }

    [HarmonyPatch(typeof(Connection), nameof(Connection.SendSignal)), HarmonyPostfix]
    public static void Connection_SendSignal_Post(Connection __instance, Signal signal)
    {
        foreach (var wire in __instance.Wires)
        {
            Connection recipient = wire.OtherConnection(__instance);
            if (recipient == null) { continue; }
            if (recipient.Item == __instance.Item || signal.source?.LastSentSignalRecipients.LastOrDefault() == recipient) { continue; }

            _eventService.PublishEvent<IEventSignalReceived>(x => x.OnSignalReceived(signal, recipient));
            _eventService.Call("signalReceived." + recipient.Item.Prefab.Identifier, signal, recipient);
        }

        foreach (CircuitBoxConnection connection in __instance.CircuitBoxConnections)
        {
            _eventService.PublishEvent<IEventSignalReceived>(x => x.OnSignalReceived(signal, connection.Connection));
            _eventService.Call("signalReceived." + connection.Connection.Item.Prefab.Identifier, signal, connection.Connection);
        }
    }

    [HarmonyPatch(typeof(Item), MethodType.Constructor, new Type[] { typeof(Rectangle), typeof(ItemPrefab), typeof(Submarine), typeof(bool), typeof(ushort) }), HarmonyPostfix]
    public static void Item_Ctor_Post(Item __instance)
    {
        _eventService.PublishEvent<IEventItemCreated>(x => x.OnItemCreated(__instance));
    }

    [HarmonyPatch(typeof(Item), nameof(Item.Remove)), HarmonyPostfix]
    public static void Item_Remove_Post(Item __instance)
    {
        _eventService.PublishEvent<IEventItemRemoved>(x => x.OnItemRemoved(__instance));
    }

    [HarmonyPatch(typeof(Item), nameof(Item.Remove)), HarmonyPostfix]
    public static void Item_ShallowRemove_Post(Item __instance)
    {
        _eventService.PublishEvent<IEventItemRemoved>(x => x.OnItemRemoved(__instance));
    }

    [HarmonyPatch(typeof(Item), nameof(Item.Use)), HarmonyPrefix]
    public static bool Item_Use_Pre(Item __instance, Character user, Limb targetLimb, Entity useTarget)
    {
        if (__instance.RequireAimToUse && (user == null || !user.IsKeyDown(InputType.Aim)))
        {
            return true;
        }

        if (__instance.Condition <= 0.0f) { return true; }

        bool? result = null;
        _eventService.PublishEvent<IEventItemUse>(x => result = x.OnItemUsed(__instance, user, targetLimb, useTarget));
        if (result == true)
        {
            return false; // skip
        }

        return true;
    }

    [HarmonyPatch(typeof(Item), nameof(Item.SecondaryUse)), HarmonyPrefix]
    public static bool Item_SecondaryUse_Pre(Item __instance, Character character)
    {
        if (__instance.Condition <= 0.0f) { return true; }

        bool? result = null;
        _eventService.PublishEvent<IEventItemSecondaryUse>(x => result = x.OnItemSecondaryUsed(__instance, character));
        if (result == true)
        {
            return false; // skip
        }

        return true;
    }

    [HarmonyPatch(typeof(Inventory), "PutItem"), HarmonyPrefix]
    public static bool Inventory_PutItem_Prefix(Inventory __instance, Item item, int i, Character user, bool removeItem)
    {
        bool? result = null;
        _eventService.PublishEvent<IEventInventoryPutItem>(x => result = x.OnInventoryPutItem(__instance, item, user, i, removeItem));
        if (result == true)
        {
            return false; // skip
        }

        return true;
    }

    [HarmonyPatch(typeof(Inventory), "TrySwapping"), HarmonyPrefix]
    public static bool Inventory_TrySwapping_Prefix(Inventory __instance, Item item, int index, Character user, bool swapWholeStack, ref bool __result)
    {
        // uncomment when we are plugin
        // if (item?.ParentInventory == null || !__instance.slots[index].Any()) { return false; }
        // if (__instance.slots[index].Items.Any(it => !it.IsInteractable(user))) { return false; }
        if (!__instance.AllowSwappingContainedItems) { return false; }

        bool? result = null;
        _eventService.PublishEvent<IEventInventoryItemSwap>(x => result = x.OnInventoryItemSwap(__instance, item, user, index, swapWholeStack));
        if (result != null)
        {
            __result = (bool)result;
            return false; // skip
        }

        return true;
    }

    public void Dispose()
    {
        IsDisposed = true;
        Harmony.UnpatchSelf();
    }

#if SERVER
    [HarmonyPatch]
    class Patch_StartGame_End
    {
        static MethodBase TargetMethod()
        {
            var original = AccessTools.Method(
                typeof(GameServer),
                "StartGame"
            );

            return AccessTools.EnumeratorMoveNext(original);
        }

        [HarmonyPostfix]
        static void Postfix(object __instance, bool __result)
        {
            if (!__result) { return; }

            var enumerator = __instance as IEnumerator<CoroutineStatus>;
            if (enumerator == null) { return; }

            if (enumerator.Current == CoroutineStatus.Success)
            {
                _eventService.PublishEvent<IEventRoundStarted>(x => x.OnRoundStart());
            }
        }
    }
#endif
}
