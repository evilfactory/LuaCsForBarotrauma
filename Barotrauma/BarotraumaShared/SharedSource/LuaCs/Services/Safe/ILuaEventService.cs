using System;
using Barotrauma.LuaCs.Services.Compatibility;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ILuaEventService : ILuaService, ILuaCsHook
{
    Guid Add(string methodId, string identifier, LuaCsFunc callback);
    Guid Add(string methodId, LuaCsFunc callback);
    void RemoveAll(string methodId, string identifier);
}
