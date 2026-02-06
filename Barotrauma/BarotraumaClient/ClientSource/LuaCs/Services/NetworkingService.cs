using Barotrauma.LuaCs;
using Barotrauma.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Barotrauma.LuaCs;

partial class NetworkingService : INetworkingService
{
    private ConcurrentDictionary<ushort, ConcurrentQueue<IReadMessage>> receiveQueue = new();

    public void SendSyncMessage()
    {
        if (GameMain.Client == null) { return; }

        WriteOnlyMessage message = new WriteOnlyMessage();
        message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);
        message.WriteByte((byte)LuaCsClientToServer.RequestAllIds);
        GameMain.Client.ClientPeer.Send(message, DeliveryMethod.Reliable);
    }

    public void NetMessageReceived(IReadMessage netMessage, ServerPacketHeader header, Client client = null)
    {
        if (header != ServerPacketHeader.LUA_NET_MESSAGE)
        {
            return;
        }

        LuaCsServerToClient luaCsHeader = (LuaCsServerToClient)netMessage.ReadByte();

        switch (luaCsHeader)
        {
            case LuaCsServerToClient.NetMessageString:
                HandleNetMessageString(netMessage);
                break;

            case LuaCsServerToClient.NetMessageId:
                HandleNetMessageId(netMessage);
                break;

            case LuaCsServerToClient.ReceiveIds:
                ReadIds(netMessage);
                break;
        }
    }

    public void NetMessageReceived(IReadMessage message, ServerPacketHeader header)
    {
        throw new NotImplementedException();
    }

    public IWriteMessage Start(Guid netId)
    {
        var message = new WriteOnlyMessage();

        message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);

        if (idToPacket.ContainsKey(netId))
        {
            message.WriteByte((byte)LuaCsClientToServer.NetMessageId);
            message.WriteUInt16(idToPacket[netId]);
        }
        else
        {
            message.WriteByte((byte)LuaCsClientToServer.NetMessageString);
            message.WriteBytes(netId.ToByteArray(), 0, 16);
        }

        return message;
    }

    public void RequestId(Guid netId)
    {
        if (idToPacket.ContainsKey(netId)) { return; }

        if (GameMain.Client == null) { return; }

        WriteOnlyMessage message = new WriteOnlyMessage();
        message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);
        message.WriteByte((byte)LuaCsClientToServer.RequestSingleId);

        message.WriteBytes(netId.ToByteArray(), 0, 16);

        Send(message, DeliveryMethod.Reliable);
    }

    public void Send(IWriteMessage netMessage, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable)
    {
        GameMain.Client.ClientPeer.Send(netMessage, deliveryMethod);
    }

    private void HandleNetMessageId(IReadMessage netMessage, Client client = null)
    {
        ushort id = netMessage.ReadUInt16();

        if (packetToId.ContainsKey(id))
        {
            HandleNetMessage(netMessage, packetToId[id], client);
        }
        else
        {
            if (!receiveQueue.ContainsKey(id)) { receiveQueue[id] = new ConcurrentQueue<IReadMessage>(); }
            receiveQueue[id].Enqueue(netMessage);

            if (GameSettings.CurrentConfig.VerboseLogging)
            {
                LuaCsLogger.LogMessage($"Received NetMessage with unknown id {id} from server, storing in queue in case we receive the id later.");
            }
        }
    }

    private void ReadIds(IReadMessage netMessage)
    {
        ushort size = netMessage.ReadUInt16();

        for (int i = 0; i < size; i++)
        {
            ushort packetId = netMessage.ReadUInt16();
            Guid netId = new Guid(netMessage.ReadBytes(16));

            packetToId[packetId] = netId;
            idToPacket[netId] = packetId;

            if (!receiveQueue.ContainsKey(packetId))
            {
                continue;
            }

            // We could have received messages before receiving the sync message, so we need to process them now

            while (receiveQueue[packetId].TryDequeue(out var queueMessage))
            {
                if (netReceives.ContainsKey(netId))
                {
                    netReceives[netId](queueMessage);
                }
            }
        }
    }
}
