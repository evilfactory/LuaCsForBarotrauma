using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Compatibility;
using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Barotrauma.LuaCs;

public partial class NetworkingService : INetworkingService
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
        RequestAllNetIds,
    }

    private enum ServerToClient
    {
        NetMessageInternalId,
        NetMessageNetId,
        ReceiveNetIds
    }

    private Dictionary<NetId, NetMessageReceived> netReceives = new Dictionary<NetId, NetMessageReceived>();
    private Dictionary<ushort, NetId> packetToId = new Dictionary<ushort, NetId>();
    private Dictionary<NetId, ushort> idToPacket = new Dictionary<NetId, ushort>();

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

    public NetworkingService(IEventService eventService, ILoggerService loggerService)
    {
        _eventService = eventService;
        _loggerService = loggerService;

#if SERVER
        IsSynchronized = true;
#endif

        SubscribeToEvents();
    }

    public void Receive(string netIdString, NetMessageReceived callback) => Receive(new NetId(netIdString), callback);
    public void Receive(Guid netIdGuid, NetMessageReceived callback) => Receive(new NetId(netIdGuid.ToString()), callback);
    public IWriteMessage Start(string netIdString) => Start(new NetId(netIdString));
    public IWriteMessage Start(Guid netIdGuid) => Start(new NetId(netIdGuid.ToString()));

    public void Receive(NetId netId, NetMessageReceived callback)
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
                netReceives[netId](netMessage, client.Connection);
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
        throw new NotImplementedException();
    }

    public void RegisterNetVar(INetworkSyncVar netVar)
    {
        throw new NotImplementedException();
    }

    public void SendNetVar(INetworkSyncVar netVar)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result Reset()
    {
        IsSynchronized = false;
        netReceives = new Dictionary<NetId, NetMessageReceived>();
        packetToId = new Dictionary<ushort, NetId>();
        idToPacket = new Dictionary<NetId, ushort>();
        SubscribeToEvents();
        return FluentResults.Result.Ok();
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
