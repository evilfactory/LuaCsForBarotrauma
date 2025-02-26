using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Barotrauma.LuaCs.Services;

partial class NetworkingService : INetworkingService
{
    private const int MaxRegisterPerClient = 1000;

    private Dictionary<string, int> clientRegisterCount = new Dictionary<string, int>();

    private ushort currentId = 0;

    public INetWriteMessage Start(Guid netId)
    {
        var message = new WriteOnlyMessage();

        message.WriteByte((byte)ServerPacketHeader.LUA_NET_MESSAGE);

        if (idToPacket.ContainsKey(netId))
        {
            message.WriteByte((byte)LuaCsServerToClient.NetMessageId);
            message.WriteUInt16(idToPacket[netId]);
        }
        else
        {
            message.WriteByte((byte)LuaCsServerToClient.NetMessageString);
            message.WriteBytes(netId.ToByteArray(), 0, 16);
        }

        return message.ToNetWriteMessage();
    }

    public void NetMessageReceived(IReadMessage netMessage, ClientPacketHeader header, Client client = null)
    {
        if (header != ClientPacketHeader.LUA_NET_MESSAGE)
        {
            return;
        }

        LuaCsClientToServer luaCsHeader = (LuaCsClientToServer)netMessage.ReadByte();

        switch (luaCsHeader)
        {
            case LuaCsClientToServer.NetMessageString:
                HandleNetMessageString(netMessage, client);
                break;

            case LuaCsClientToServer.NetMessageId:
                HandleNetMessageId(netMessage, client);
                break;

            case LuaCsClientToServer.RequestAllIds:
                WriteAllIds(client);
                break;

            case LuaCsClientToServer.RequestSingleId:
                RequestIdSingle(netMessage, client);
                break;
        }
    }

    private void HandleNetMessageId(IReadMessage netMessage, Client client = null)
    {
        ushort id = netMessage.ReadUInt16();

        if (packetToId.ContainsKey(id))
        {
            Guid netId = packetToId[id];

            HandleNetMessage(netMessage, netId, client);
        }
        else
        {
            if (GameSettings.CurrentConfig.VerboseLogging)
            {
                LuaCsLogger.LogError($"Received NetMessage for unknown id {id} from {GameServer.ClientLogName(client)}.");
            }
        }
    }

    private ushort RegisterId(Guid netId)
    {
        if (idToPacket.ContainsKey(netId))
        {
            return idToPacket[netId];
        }

        if (currentId >= ushort.MaxValue)
        {
            LuaCsLogger.LogError($"Tried to register more than {ushort.MaxValue} network ids!");
            return 0;
        }

        currentId++;

        packetToId[currentId] = netId;
        idToPacket[netId] = currentId;

        WriteIdToAll(currentId, netId);

        return currentId;
    }

    private void RequestIdSingle(IReadMessage netMessage, Client client)
    {
        Guid netId = new Guid(netMessage.ReadBytes(16));

        if (!idToPacket.ContainsKey(netId) && client.AccountId.TryUnwrap(out AccountId id))
        {
            if (!clientRegisterCount.ContainsKey(id.StringRepresentation))
            {
                clientRegisterCount[id.StringRepresentation] = 0;
            }

            clientRegisterCount[id.StringRepresentation]++;

            if (clientRegisterCount[id.StringRepresentation] > MaxRegisterPerClient)
            {
                LuaCsLogger.Log($"{GameServer.ClientLogName(client)} Tried to register more than {MaxRegisterPerClient} Ids!");
                return;
            }
        }

        RegisterId(netId);
    }

    private void WriteIdToAll(ushort packet, Guid netId)
    {
        WriteOnlyMessage message = new WriteOnlyMessage();
        message.WriteByte((byte)ServerPacketHeader.LUA_NET_MESSAGE);
        message.WriteByte((byte)LuaCsServerToClient.ReceiveIds);

        message.WriteUInt16(1);
        message.WriteUInt16(packet);
        message.WriteBytes(netId.ToByteArray(), 0, 16);

        Send(message, null, DeliveryMethod.Reliable);
    }

    private void WriteAllIds(Client client)
    {
        WriteOnlyMessage message = new WriteOnlyMessage();
        message.WriteByte((byte)ServerPacketHeader.LUA_NET_MESSAGE);
        message.WriteByte((byte)LuaCsServerToClient.ReceiveIds);

        message.WriteUInt16((ushort)packetToId.Count());
        foreach ((ushort packet, Guid netId) in packetToId)
        {
            message.WriteUInt16(packet);
            message.WriteBytes(netId.ToByteArray(), 0, 16);
        }

        Send(message, client.Connection, DeliveryMethod.Reliable);
    }

    public void ClientWriteLobby(Client client) => GameMain.Server.ClientWriteLobby(client);

    public void Send(IWriteMessage netMessage, NetworkConnection connection = null, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable)
    {
        if (connection == null)
        {
            foreach (NetworkConnection conn in Client.ClientList.Select(c => c.Connection))
            {
                GameMain.Server.ServerPeer.Send(netMessage, conn, deliveryMethod);
            }
        }
        else
        {
            GameMain.Server.ServerPeer.Send(netMessage, connection, deliveryMethod);
        }
    }
}
