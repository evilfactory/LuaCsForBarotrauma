using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Barotrauma.LuaCs;

partial class NetworkingService : INetworkingService, IEventClientRawNetMessageReceived
{
    private const int MaxRegisterPerClient = 1000;

    private Dictionary<string, int> clientRegisterCount = new Dictionary<string, int>();

    private ushort currentId = 0;

    public IWriteMessage Start(NetId netId)
    {
        var message = new WriteOnlyMessage();

        message.WriteByte((byte)ServerPacketHeader.LUA_NET_MESSAGE);

        if (idToPacket.ContainsKey(netId))
        {
            message.WriteByte((byte)ServerToClient.NetMessageInternalId);
            message.WriteUInt16(idToPacket[netId]);
        }
        else
        {
            message.WriteByte((byte)ServerToClient.NetMessageNetId);
            NetId.Write(message, netId);
        }

        return message;
    }

    public void OnReceivedClientNetMessage(IReadMessage netMessage, ClientPacketHeader serverPacketHeader, NetworkConnection sender)
    {
        if (serverPacketHeader != ClientPacketHeader.LUA_NET_MESSAGE)
        {
            return;
        }

        Client client = GameMain.Server.ConnectedClients.First(c => c.Connection == sender);

        ClientToServer luaCsHeader = (ClientToServer)netMessage.ReadByte();

        switch (luaCsHeader)
        {
            case ClientToServer.NetMessageNetId:
                HandleNetMessageString(netMessage, client);
                break;

            case ClientToServer.NetMessageInternalId:
                HandleNetMessageId(netMessage, client);
                break;

            case ClientToServer.RequestAllNetIds:
                WriteAllIds(client);
                break;

            case ClientToServer.RequestSingleNetId:
                RequestIdSingle(netMessage, client);
                break;
        }
    }

    private void HandleNetMessageId(IReadMessage netMessage, Client client = null)
    {
        ushort id = netMessage.ReadUInt16();

        if (packetToId.ContainsKey(id))
        {
            NetId netId = packetToId[id];

            HandleNetMessage(netMessage, netId, client);
        }
        else
        {
            if (GameSettings.CurrentConfig.VerboseLogging)
            {
                _loggerService.LogError($"Received NetMessage for unknown id {id} from {GameServer.ClientLogName(client)}.");
            }
        }
    }

    private ushort RegisterId(NetId netId)
    {
        if (idToPacket.ContainsKey(netId))
        {
            return idToPacket[netId];
        }

        if (currentId >= ushort.MaxValue)
        {
            _loggerService.LogError($"Tried to register more than {ushort.MaxValue} network ids!");
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
        NetId netId = NetId.Read(netMessage);

        if (!idToPacket.ContainsKey(netId) && client.AccountId.TryUnwrap(out AccountId id))
        {
            if (!clientRegisterCount.ContainsKey(id.StringRepresentation))
            {
                clientRegisterCount[id.StringRepresentation] = 0;
            }

            clientRegisterCount[id.StringRepresentation]++;

            if (clientRegisterCount[id.StringRepresentation] > MaxRegisterPerClient)
            {
                _loggerService.Log($"{GameServer.ClientLogName(client)} Tried to register more than {MaxRegisterPerClient} Ids!");
                return;
            }
        }

        RegisterId(netId);
    }

    private void WriteIdToAll(ushort packet, NetId netId)
    {
        WriteOnlyMessage message = new WriteOnlyMessage();
        message.WriteByte((byte)ServerPacketHeader.LUA_NET_MESSAGE);
        message.WriteByte((byte)ServerToClient.ReceiveNetIds);

        message.WriteUInt16(1);
        message.WriteUInt16(packet);
        NetId.Write(message, netId);

        SendToClient(message, null, DeliveryMethod.Reliable);
    }

    private void WriteAllIds(Client client)
    {
        WriteOnlyMessage message = new WriteOnlyMessage();
        message.WriteByte((byte)ServerPacketHeader.LUA_NET_MESSAGE);
        message.WriteByte((byte)ServerToClient.ReceiveNetIds);

        message.WriteUInt16((ushort)packetToId.Count());
        foreach ((ushort packet, NetId netId) in packetToId)
        {
            message.WriteUInt16(packet);
            NetId.Write(message, netId);
        }

        SendToClient(message, client.Connection, DeliveryMethod.Reliable);
    }

    public void SendToClient(IWriteMessage netMessage, NetworkConnection connection = null, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable)
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
