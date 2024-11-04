using System;
using System.Collections.Generic;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Compatibility;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ILuaSafeEventService : ILuaService, ILuaCsHook
{
    void Subscribe(string interfaceName, string identifier, IDictionary<string, LuaCsFunc> callbacks);
    /// <summary>
    /// Removes a subscriber from an event that subscribed under the given identifier.
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="identifier"></param>
    void Remove(string eventName, string identifier);
    /// <summary>
    /// Send an event to all subscribers to an interface.
    /// </summary>
    /// <param name="interfaceName">Name of the interface (must be registered with Lua).</param>
    /// <param name="runner">Execution runner, the subscriber is provided as the first argument in the lua runner.</param>
    /// <returns></returns>
    void PublishLuaEvent(string interfaceName, LuaCsFunc runner);
}

public interface ILuaEventService : ILuaSafeEventService
{
    public FluentResults.Result RegisterSafeEvent<T>() where T : IEvent<T>;
    public FluentResults.Result UnregisterSafeEvent<T>() where T : IEvent<T>;
}
