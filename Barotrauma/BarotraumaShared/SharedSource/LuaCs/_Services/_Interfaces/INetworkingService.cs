using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Compatibility;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs;

#if CLIENT
public delegate void NetMessageReceived(IReadMessage netMessage);
#elif SERVER
internal delegate void NetMessageReceived(IReadMessage netMessage, Client connection);
#endif

internal interface INetworkingService : IReusableService, ILuaCsNetworking, IEntityNetworkingService
{
    bool IsActive { get; }
    bool IsSynchronized { get; }

    IWriteMessage Start(string netId);
    IWriteMessage Start(Guid netId);
    void Receive(string netId, NetMessageReceived action);
    void Receive(Guid netId, NetMessageReceived action);
#if SERVER
    void SendToClient(IWriteMessage netMessage, NetworkConnection connection = null, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable);
#elif CLIENT
    void SendToServer(IWriteMessage netMessage, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable);
#endif

}

public interface IEntityNetworkingService
{
    Guid GetNetworkIdForInstance(INetworkSyncVar var);
    void RegisterNetVar(INetworkSyncVar netVar);
    void SendNetVar(INetworkSyncVar netVar);
    void SendNetVar(INetworkSyncVar netVar, NetworkConnection connection);
}
