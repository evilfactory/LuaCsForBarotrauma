using System;
using Barotrauma.LuaCs.Services.Compatibility;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ILuaEventService : ILuaService, ILuaCsHook
{
    void Add(string eventName, string identifier, LuaCsFunc callback);
    void Add(string eventName, LuaCsFunc callback);
    void Remove(string eventName, string identifier);
}
