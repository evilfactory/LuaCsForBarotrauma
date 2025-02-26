using System;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

public interface INetVar : IVarId
{
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
}

public enum NetSync
{
    None, TwoWay, ServerAuthority, ClientOneWay
}
