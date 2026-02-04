using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

internal delegate void NetMessageReceived(IReadMessage netMessage);

internal partial interface INetworkingService : IReusableService, ILuaCsNetworking, IEntityNetworkingService
{
    bool IsActive { get; }
    bool IsSynchronized { get; }

    public IWriteMessage Start(Guid netId);
    public void Receive(Guid netId, NetMessageReceived action);
#if SERVER
    public void Send(IWriteMessage netMessage, NetworkConnection connection = null, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable);
#elif CLIENT
    public void Send(IWriteMessage netMessage, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable);
#endif
    
}

public interface IEntityNetworkingService
{
    public void RegisterNetVar(INetworkSyncEntity netVar);
    public void SendNetVar(INetworkSyncEntity netVar);
}
