﻿using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Networking;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

internal interface INetworkingService : IReusableService, ILuaCsNetworking
{
    bool IsActive { get; }
    bool IsSynchronized { get; }
    bool TryRegisterVar(INetVar var, NetSync mode, ClientPermissions permissions);
    void UnregisterVar(Guid varId);
    bool SendEvent(Guid varId);
    void SendMessageGlobal(string id, string message);
    void Synchronize();

    #region LegacyAPI

    bool RestrictMessageSize { get; set; }

    #endregion
}