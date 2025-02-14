﻿using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;
using System;
using System.Collections.Generic;

namespace Barotrauma.LuaCs.Networking;

internal partial class NetworkingService : INetworkingService
{
    private enum LuaCsClientToServer
    {
        NetMessageId,
        NetMessageString,
        RequestSingleId,
        RequestAllIds,
    }

    private enum LuaCsServerToClient
    {
        NetMessageId,
        NetMessageString,
        ReceiveIds
    }

    private Dictionary<Guid, INetVar> netVars = new Dictionary<Guid, INetVar>();
    private Dictionary<Guid, NetMessageReceived> netReceives = new Dictionary<Guid, NetMessageReceived>();
    private Dictionary<ushort, Guid> packetToId = new Dictionary<ushort, Guid>();
    private Dictionary<Guid, ushort> idToPacket = new Dictionary<Guid, ushort>();

    public bool IsActive
    {
        get
        {
            return GameMain.NetworkMember != null; // ehh?
        }
    }
    public bool IsSynchronized { get; private set; }
    public bool IsDisposed { get; private set; }

    public void Initialize()
    {
#if SERVER
        IsSynchronized = true;
#elif CLIENT
        SendSyncMessage();
#endif
    }

    public void RegisterNetVar(INetVar netVar)
    {
        netVars[netVar.InstanceId] = netVar;

        netReceives[netVar.InstanceId] = (IReadMessage netMessage) =>
        {
            INetReadMessage internalMind = new NetReadMessage();
            internalMind.SetMessage(netMessage);
            netVar.ReadNetMessage(internalMind);
        };
    }

    public void SendNetVar(INetVar netVar)
    {
        if (netVars.ContainsKey(netVar.InstanceId))
        {
            INetWriteMessage message = Start(netVar.InstanceId);
            netVar.WriteNetMessage(message);
            Send(message.Message);
        }
    }

    public void Receive(Guid netId, NetMessageReceived callback)
    {
#if SERVER
        RegisterId(netId);
#elif CLIENT
        RequestId(netId);
#endif
        netReceives[netId] = callback;
    }

    private void HandleNetMessage(IReadMessage netMessage, Guid netId, Client client = null)
    {
        if (netReceives.ContainsKey(netId))
        {
            try
            {
                netReceives[netId](netMessage);
            }
            catch (Exception e)
            {
                LuaCsLogger.LogError($"Exception thrown inside NetMessageReceive({netId})", LuaCsMessageOrigin.CSharpMod);
                LuaCsLogger.HandleException(e, LuaCsMessageOrigin.CSharpMod);
            }
        }
        else
        {
            if (GameSettings.CurrentConfig.VerboseLogging)
            {
#if SERVER
                LuaCsLogger.LogError($"Received NetMessage for unknown netid {netId} from {GameServer.ClientLogName(client)}.");
#else
                LuaCsLogger.LogError($"Received NetMessage for unknown netid {netId} from server.");
#endif
            }
        }
    }

    private void HandleNetMessageString(IReadMessage netMessage, Client client = null)
    {
        Guid guid = new Guid(netMessage.ReadBytes(16));

        HandleNetMessage(netMessage, guid, client);
    }

    public FluentResults.Result Reset()
    {
        IsSynchronized = false;
        netReceives = new Dictionary<Guid, NetMessageReceived>();
        packetToId = new Dictionary<ushort, Guid>();
        idToPacket = new Dictionary<Guid, ushort>();
        return FluentResults.Result.Ok();
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
