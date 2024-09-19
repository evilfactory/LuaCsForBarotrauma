using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Networking;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Networking;

public interface INetVar : IVarId
{
    /// <summary>
    /// Synchronized network id, uninitialized if value is zero/0. Used by Networking service.
    /// </summary>
    ushort NetId { get; }
    /// <summary>
    /// Synchronization type
    /// </summary>
    NetSync SyncType { get; }
    /// <summary>
    /// Permissions needed by clients to send net-events or receive net messages. 
    /// </summary>
    ClientPermissions WritePermissions { get; }
    void ReadNetMessage(INetReadMessage message);
    void WriteNetMessage(INetWriteMessage message);
    void Initialize(ushort netId, NetSync syncMode, ClientPermissions writePermissions);
}
