using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Compatibility;
using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using FluentResults;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Barotrauma.LuaCs;

internal partial class NetworkingService : INetworkingService
{
    public readonly record struct NetId
    {
        private readonly string _value;

        public NetId(string netId)
        {
            _value = netId;
        }

        public static void Write(IWriteMessage message, NetId netId)
        {
            message.WriteString(netId._value);
        }

        public static NetId Read(IReadMessage message)
        {
            return new NetId(message.ReadString());
        }
    }

    private enum ClientToServer
    {
        NetMessageInternalId,
        NetMessageNetId,
        RequestSingleNetId,
        RequestSync,
    }

    private enum ServerToClient
    {
        NetMessageInternalId,
        NetMessageNetId,
        ReceiveNetIds
    }


    private ConcurrentDictionary<INetworkSyncVar, NetId> netVars = [];

    private ConcurrentDictionary<NetId, NetMessageReceived> netReceives = [];
    private ConcurrentDictionary<ushort, NetId> packetToId = [];
    private ConcurrentDictionary<NetId, ushort> idToPacket = [];

    public bool IsActive
    {
        get
        {
            return GameMain.NetworkMember != null;
        }
    }

    public bool IsSynchronized { get; private set; }
    public bool IsDisposed { get; private set; }

    private readonly IEventService _eventService;
    private readonly ILoggerService _loggerService;
    private readonly INetworkIdProvider _networkIdProvider;

    public NetworkingService(IEventService eventService, INetworkIdProvider networkIdProvider, ILoggerService loggerService)
    {
        _eventService = eventService;
        _networkIdProvider = networkIdProvider;
        _loggerService = loggerService;

#if SERVER
        IsSynchronized = true;
#endif

        SubscribeToEvents();
    }

    public void Receive(string netIdString, LuaCsAction callback)
    {
#if SERVER
        Receive(new NetId(netIdString), (IReadMessage message, Client client) => callback(message, client));
#elif CLIENT
        Receive(new NetId(netIdString), (IReadMessage message) => callback(message, null));
#endif
    }

    public void Receive(string netIdString, NetMessageReceived callback) => Receive(new NetId(netIdString), callback);
    public void Receive(Guid netIdGuid, NetMessageReceived callback) => Receive(new NetId(netIdGuid.ToString()), callback);
    public IWriteMessage Start(string netIdString) => Start(new NetId(netIdString));
    public IWriteMessage Start(Guid netIdGuid) => Start(new NetId(netIdGuid.ToString()));

    internal void Receive(NetId netId, NetMessageReceived callback)
    {
#if SERVER
        RegisterId(netId);
#elif CLIENT
        RequestId(netId);
#endif
        netReceives[netId] = callback;
    }

    private void HandleNetMessage(IReadMessage netMessage, NetId netId, Client client = null)
    {
        if (netReceives.ContainsKey(netId))
        {
            try
            {
#if CLIENT
                netReceives[netId](netMessage);
#elif SERVER
                netReceives[netId](netMessage, client);
#endif
            }
            catch (Exception e)
            {
                _loggerService.LogResults(new ExceptionalError("Exception thrown inside NetMessageReceive({netId})", e));
            }
        }
        else
        {
            if (GameSettings.CurrentConfig.VerboseLogging)
            {
#if SERVER
                _loggerService.LogError($"Received NetMessage for unknown netid {netId} from {GameServer.ClientLogName(client)}.");
#else
                _loggerService.LogError($"Received NetMessage for unknown netid {netId} from server.");
#endif
            }
        }
    }

    private void HandleNetMessageString(IReadMessage netMessage, Client client = null)
    {
        NetId netId = NetId.Read(netMessage);

        HandleNetMessage(netMessage, netId, client);
    }

    private void SubscribeToEvents()
    {
#if CLIENT
        _eventService.Subscribe<IEventConnectedToServer>(this);
        _eventService.Subscribe<IEventServerRawNetMessageReceived>(this);
#elif SERVER
        _eventService.Subscribe<IEventClientRawNetMessageReceived>(this);
#endif
    }

    public Guid GetNetworkIdForInstance(INetworkSyncVar var)
    {
        return _networkIdProvider.GetNetworkIdForInstance(var);
    }

    public void RegisterNetVar(INetworkSyncVar netVar)
    {
        netVar.SetNetworkOwner(this);

        NetId netId = new NetId(netVar.InstanceId.ToString());
        netVars[netVar] = netId;

#if CLIENT
        Receive(netId, (IReadMessage message) =>
        {
            if (netVar.SyncType == NetSync.None)
            {
                _loggerService.LogWarning($"Received net var from server but {nameof(NetSync)} is {netVar.SyncType.ToString()}");
                return;
            }

            netVar.ReadNetMessage(message);
        });
#elif SERVER
        Receive(netId, (IReadMessage message, Client client) =>
        {
            if (netVar.SyncType == NetSync.None || netVar.SyncType == NetSync.ServerAuthority)
            {
                _loggerService.LogWarning($"Received net var from {GameServer.ClientLogName(client)} but {nameof(NetSync)} is {netVar.SyncType.ToString()}");
                return;
            }

            if (!client.HasPermission(netVar.WritePermissions))
            {
                _loggerService.LogWarning($"Received net var from {GameServer.ClientLogName(client)} but the client lacks permissions to modify it");
                return;
            }

            netVar.ReadNetMessage(message);

            // Sync back to all clients
            if (netVar.SyncType != NetSync.ClientOneWay)
            {
                SendNetVar(netVar);
            }
        });
#endif
    }

    public void SendNetVar(INetworkSyncVar netVar) => SendNetVar(netVar);

    public void SendNetVar(INetworkSyncVar netVar, NetworkConnection connection = null)
    {
        if (!netVars.TryGetValue(netVar, out NetId netId))
        {
            throw new InvalidOperationException("Tried to send net var across network without registering first");
        }

        if (netVar.SyncType == NetSync.None) { return; }
#if CLIENT
        if (netVar.SyncType == NetSync.ServerAuthority) { return; }
#elif SERVER
        if (netVar.SyncType == NetSync.ClientOneWay) { return; }
#endif

        IWriteMessage message = Start(netId);
        netVar.WriteNetMessage(message);
#if CLIENT
        SendToServer(message);
#elif SERVER
        SendToClient(message);
#endif
    }

    public FluentResults.Result Reset()
    {
        IsSynchronized = false;
        netReceives = new ConcurrentDictionary<NetId, NetMessageReceived>();
        packetToId = new ConcurrentDictionary<ushort, NetId>();
        idToPacket = new ConcurrentDictionary<NetId, ushort>();
        netVars = new ConcurrentDictionary<INetworkSyncVar, NetId>();

        SubscribeToEvents();
        return FluentResults.Result.Ok();
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
