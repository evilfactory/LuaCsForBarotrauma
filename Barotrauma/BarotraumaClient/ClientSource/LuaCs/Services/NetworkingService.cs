using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Barotrauma.LuaCs;

partial class NetworkingService : INetworkingService, IEventConnectedToServer, IEventServerRawNetMessageReceived
{
    private ConcurrentDictionary<ushort, ConcurrentQueue<IReadMessage>> receiveQueue = new();

    public void OnConnectedToServer()
    {
        SendSyncMessage();
    }

    public void OnReceivedServerNetMessage(IReadMessage netMessage, ServerPacketHeader serverPacketHeader)
    {
        if (serverPacketHeader != ServerPacketHeader.LUA_NET_MESSAGE)
        {
            return;
        }

        ServerToClient luaCsHeader = (ServerToClient)netMessage.ReadByte();

        switch (luaCsHeader)
        {
            case ServerToClient.NetMessageNetId:
                HandleNetMessageString(netMessage);
                break;

            case ServerToClient.NetMessageInternalId:
                HandleNetMessageId(netMessage);
                break;

            case ServerToClient.ReceiveNetIds:
                ReadIds(netMessage);
                break;
        }
    }

    private void SendSyncMessage()
    {
        if (GameMain.Client == null) { return; }

        WriteOnlyMessage message = new WriteOnlyMessage();
        message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);
        message.WriteByte((byte)ClientToServer.RequestAllNetIds);
        GameMain.Client.ClientPeer.Send(message, DeliveryMethod.Reliable);
    }

    public IWriteMessage Start(NetId netId)
    {
        var message = new WriteOnlyMessage();

        message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);

        if (idToPacket.ContainsKey(netId))
        {
            message.WriteByte((byte)ClientToServer.NetMessageInternalId);
            message.WriteUInt16(idToPacket[netId]);
        }
        else
        {
            message.WriteByte((byte)ClientToServer.NetMessageNetId);
            NetId.Write(message, netId);
        }

        return message;
    }

    public void SendToServer(IWriteMessage netMessage, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable)
    {
        GameMain.Client.ClientPeer.Send(netMessage, deliveryMethod);
    }

    private void RequestId(NetId netId)
    {
        if (idToPacket.ContainsKey(netId)) { return; }

        if (GameMain.Client == null) { return; }

        WriteOnlyMessage message = new WriteOnlyMessage();
        message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);
        message.WriteByte((byte)ClientToServer.RequestSingleNetId);

        NetId.Write(message, netId);

        SendToServer(message, DeliveryMethod.Reliable);
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
                _loggerService.LogMessage($"Received NetMessage with unknown id {id} from server, storing in queue in case we receive the id later.");
            }
        }
    }

    private void ReadIds(IReadMessage netMessage)
    {
        ushort size = netMessage.ReadUInt16();

        for (int i = 0; i < size; i++)
        {
            ushort packetId = netMessage.ReadUInt16();
            NetId netId = NetId.Read(netMessage);

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
