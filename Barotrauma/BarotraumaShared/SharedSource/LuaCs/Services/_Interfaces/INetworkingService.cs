using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Networking;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

internal delegate void NetMessageReceived(IReadMessage netMessage);

internal interface INetworkingService : IReusableService, ILuaCsNetworking
{
    bool IsActive { get; }
    bool IsSynchronized { get; }

    public INetWriteMessage Start(Guid netId);
    public void Receive(Guid netId, NetMessageReceived action);
#if SERVER
    public void Send(IWriteMessage netMessage, NetworkConnection connection = null, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable);
#elif CLIENT
    public void Send(IWriteMessage netMessage, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable);
#endif
    public void RegisterNetVar(INetVar netVar);
    public void SendNetVar(INetVar netVar);
}
