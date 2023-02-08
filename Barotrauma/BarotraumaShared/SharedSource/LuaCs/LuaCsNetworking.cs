using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Barotrauma.Networking;

namespace Barotrauma
{
    partial class LuaCsNetworking
    {
        private static readonly HttpClient client = new HttpClient();

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

        public bool RestrictMessageSize = true;

        private Dictionary<string, LuaCsAction> netReceives = new Dictionary<string, LuaCsAction>();
        private Dictionary<ushort, string> idToString = new Dictionary<ushort, string>();
        private Dictionary<string, ushort> stringToId = new Dictionary<string, ushort>();

        public void Initialize()
        {
#if CLIENT
            SendSyncMessage();
#endif
        }

        public void Remove(string netMessageName)
        {
            netReceives.Remove(netMessageName);
        }

        public IWriteMessage Start()
        {
            return new WriteOnlyMessage();
        }

        public string IdToString(ushort id)
        {
            if (idToString.ContainsKey(id)) { return idToString[id]; }

            return null;
        }

        public ushort StringToId(string name)
        {
            if (stringToId.ContainsKey(name)) { return stringToId[name]; }

            return 0;
        }

        private void HandleNetMessage(IReadMessage netMessage, string name, Client client = null)
        {
            if (netReceives.ContainsKey(name))
            {
                try
                {
                    netReceives[name](netMessage, client);
                }
                catch (Exception e)
                {
                    LuaCsLogger.LogError($"Exception thrown inside NetMessageReceive({name})", LuaCsMessageOrigin.CSharpMod);
                    LuaCsLogger.HandleException(e, LuaCsMessageOrigin.CSharpMod);
                }
            }
            else
            {
                if (GameSettings.CurrentConfig.VerboseLogging)
                {
#if SERVER
                    LuaCsLogger.LogError($"Received NetMessage for unknown name {name} from {GameServer.ClientLogName(client)}.");
#else
                    LuaCsLogger.LogError($"Received NetMessage for unknown name {name} from server.");
#endif
                }
            }
        }

        private void HandleNetMessageString(IReadMessage netMessage, Client client = null)
        {
            string name = netMessage.ReadString();

            HandleNetMessage(netMessage, name, client);
        }

        public async void HttpRequest(string url, LuaCsAction callback, string data = null, string method = "POST", string contentType = "application/json", Dictionary<string, string> headers = null, string savePath = null)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), url);

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                if (data != null)
                {
                    request.Content = new StringContent(data, Encoding.UTF8, contentType);
                }
                
                HttpResponseMessage response = await client.SendAsync(request);

                if (savePath != null)
                {
                    byte[] responseData = await response.Content.ReadAsByteArrayAsync();

                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                    {
                        fileStream.Write(responseData, 0, responseData.Length);
                    }
                }

                string responseBody = await response.Content.ReadAsStringAsync();

                GameMain.LuaCs.Timer.Wait((object[] par) => 
                { 
                    callback(responseBody, (int)response.StatusCode, response.Headers); 
                }, 0);
            }
            catch (HttpRequestException e)
            {
                GameMain.LuaCs.Timer.Wait((object[] par) => { callback(e.Message, e.StatusCode, null); }, 0);
            }
            catch (Exception e)
            {
                GameMain.LuaCs.Timer.Wait((object[] par) => { callback(e.Message, null, null); }, 0);
            }
        }

        public void HttpPost(string url, LuaCsAction callback, string data, string contentType = "application/json", Dictionary<string, string> headers = null)
        {
            HttpRequest(url, callback, data, "POST", contentType, headers);
        }


        public void HttpGet(string url, LuaCsAction callback, Dictionary<string, string> headers = null)
        {
            HttpRequest(url, callback, null, "GET", null, headers);
        }

        public void CreateEntityEvent(INetSerializable entity, NetEntityEvent.IData extraData)
        {
            GameMain.NetworkMember.CreateEntityEvent(entity, extraData);
        }

        public ushort LastClientListUpdateID
        {
            get { return GameMain.NetworkMember.LastClientListUpdateID; }
            set { GameMain.NetworkMember.LastClientListUpdateID = value; }
        }
    }
}
