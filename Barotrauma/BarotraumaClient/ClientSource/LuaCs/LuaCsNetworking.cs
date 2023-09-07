using Barotrauma.Networking;
using System.Collections.Generic;

namespace Barotrauma
{
    partial class LuaCsNetworking
    {
        private Dictionary<ushort, Queue<IReadMessage>> receiveQueue = new Dictionary<ushort, Queue<IReadMessage>>();

        public void SendSyncMessage()
        {
            if (GameMain.Client == null) { return; }

            WriteOnlyMessage message = new WriteOnlyMessage();
            message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);
            message.WriteByte((byte)LuaCsClientToServer.RequestAllIds);
            GameMain.Client.ClientPeer.Send(message, DeliveryMethod.ReliableOrdered);
        }

        public void NetMessageReceived(IReadMessage netMessage, ServerPacketHeader header, Client client = null)
        {
            if (header != ServerPacketHeader.LUA_NET_MESSAGE)
            {
                GameMain.LuaCs.Hook.Call("netMessageReceived", netMessage, header, client);
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

        public IWriteMessage Start(string netMessageName)
        {
            var message = new WriteOnlyMessage();

            message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);

            if (stringToId.ContainsKey(netMessageName))
            {
                message.WriteByte((byte)LuaCsClientToServer.NetMessageId);
                message.WriteUInt16(stringToId[netMessageName]);
            }
            else
            {
                message.WriteByte((byte)LuaCsClientToServer.NetMessageString);
                message.WriteString(netMessageName);
            }

            return message;
        }

        public void Receive(string netMessageName, LuaCsAction callback)
        {
            RequestId(netMessageName);

            netReceives[netMessageName] = callback;
        }

        public void RequestId(string netMessageName)
        {
            if (stringToId.ContainsKey(netMessageName)) { return; }

            if (GameMain.Client == null) { return; }

            WriteOnlyMessage message = new WriteOnlyMessage();
            message.WriteByte((byte)ClientPacketHeader.LUA_NET_MESSAGE);
            message.WriteByte((byte)LuaCsClientToServer.RequestSingleId);

            message.WriteString(netMessageName);

            Send(message, DeliveryMethod.ReliableOrdered);
        }

        public void Send(IWriteMessage netMessage, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable)
        {
            GameMain.Client.ClientPeer.Send(netMessage, deliveryMethod);
        }

        private void HandleNetMessageId(IReadMessage netMessage, Client client = null)
        {
            ushort id = netMessage.ReadUInt16();

            if (idToString.ContainsKey(id))
            {
                string name = idToString[id];

                HandleNetMessage(netMessage, name, client);
            }
            else
            {
                if (!receiveQueue.ContainsKey(id)) { receiveQueue[id] = new Queue<IReadMessage>(); }
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
                ushort id = netMessage.ReadUInt16();
                string name = netMessage.ReadString();

                idToString[id] = name;
                stringToId[name] = id;

                if (!receiveQueue.ContainsKey(id))
                {
                    continue;
                }

                while (receiveQueue[id].TryDequeue(out var queueMessage))
                {
                    if (netReceives.ContainsKey(name))
                    {
                        netReceives[name](queueMessage, null);
                    }
                }
            }
        }
    }
        
}
