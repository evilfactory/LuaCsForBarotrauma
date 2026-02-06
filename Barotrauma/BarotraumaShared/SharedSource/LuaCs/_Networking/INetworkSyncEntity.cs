using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs;

public interface INetworkSyncEntity
{
    /// <summary>
    /// Network-synchronized object ID. Used for networking send/receive message events.
    /// </summary>
    ulong InstanceId { get; }

    /// <summary>
    /// Sets the <see cref="IEntityNetworkingService"/> that is currently managing this instance. The <see cref="InstanceId"/>
    /// is retrieved from here.
    /// </summary>
    /// <param name="networkingService">The networking service managing this instance or null to deregister.</param>
    void SetNetworkOwner(IEntityNetworkingService networkingService);
    
    /// <summary>
    /// Synchronization type. See <see cref="NetSync"/> for more information.
    /// </summary>
    NetSync SyncType { get; }
    
    /// <summary>
    /// Permissions needed by clients to send net-events and/or receive net messages. 
    /// </summary>
    ClientPermissions WritePermissions { get; }

    /// <summary>
    /// Called when an incoming net message has data for this network object, typically from the same entity on another
    /// machine.
    /// </summary>
    /// <param name="message">Wrapper for the internal type: <see cref="IReadMessage"/></param>
    void ReadNetMessage(IReadMessage message);
    
    /// <summary>
    /// Called when a network send-event involving this entity is triggered. Any data expected to be read by the recipient
    /// network object on the other instance(s) should be written to the packet.  
    /// </summary>
    /// <param name="message">Wrapper for the internal type: <see cref="IWriteMessage"/></param>
    void WriteNetMessage(IWriteMessage message);
}

/// <summary>
/// Specifies the networking send/receive relationship for network object. Objects implementing this interface are
/// expected to adhere to the contract or de-sync may occur.
/// </summary>
public enum NetSync
{
    /// <summary>
    /// No network synchronization.
    /// </summary>
    None, 
    /// <summary>
    /// Both the client and the server have 'send' and 'receive' permissions (limited by <see cref="ClientPermissions"/>). Can also be used to allow two-way communication
    /// with the server.
    /// </summary>
    TwoWay, 
    /// <summary>
    /// Only the host/server has the authority to change this value.
    /// </summary>
    ServerAuthority, 
    /// <summary>
    /// Only clients (with the required by <see cref="ClientPermissions"/>) may change the value and all value changes are communicated to the server/host.
    /// <br/><br/><b>[Important] The host/server will not send the value to other connected clients.</b><br/>
    /// Intended to allow clients to send one-way messages to the server.
    /// </summary>
    ClientOneWay
}
