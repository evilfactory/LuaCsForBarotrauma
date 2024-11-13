using System;
using Barotrauma.LuaCs.Services.Compatibility;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ILuaEventService : ILuaService, ILuaCsHook
{
    void Add(string eventName, string identifier, LuaCsFunc callback);
    void Add(string eventName, LuaCsFunc callback);
    void Remove(string eventName, string identifier);
    /// <summary>
    /// Lua call
    /// </summary>
    /// <param name="interfaceName">Name of the interface (must be registered with Lua).</param>
    /// <param name="runner">Execution runner, the subscriber is provided as the first element in the array to the lua runner.</param>
    /// <returns></returns>
    FluentResults.Result PublishLuaEvent(string interfaceName, LuaCsFunc runner);
}
