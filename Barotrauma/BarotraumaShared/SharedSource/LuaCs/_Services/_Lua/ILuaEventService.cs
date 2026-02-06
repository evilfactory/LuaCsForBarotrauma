using System;
using System.Collections.Generic;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Compatibility;

namespace Barotrauma.LuaCs;

public interface ILuaSafeEventService : ILuaService, ILuaCsHook
{
    /// <summary>
    /// Subscribes lua scripts via <see cref="ImpromptuInterface"/> for the given <see cref="IEvent{T}"/> interface.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="identifier"></param>
    /// <param name="callbacks">A 'method name'=='signature action' dictionary matching the interface method list.</param>
    void Subscribe<T>(string identifier, IDictionary<string, LuaCsFunc> callbacks) where T : IEvent<T>;
    /// <summary>
    /// Removes a subscriber from an event that subscribed under the given identifier.
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="identifier"></param>
    void Remove(string eventName, string identifier);
    /// <summary>
    /// Send an event to all subscribers to an interface.
    /// </summary>
    /// <typeparam name="T">Interface type.</typeparam>
    /// <param name="subscriberRunner">Execution runner, the subscriber is provided as the first argument in the lua runner.</param>
    /// <returns></returns>
    void PublishLuaEvent<T>(LuaCsFunc subscriberRunner) where T : IEvent<T>;
    
    /// <summary>
    /// Defines the target method name for legacy <see cref="ILuaCsHook.Add(string, LuaCsFunc)"/> to target on new <see cref="IEvent{T}"/>
    /// interfaces.
    /// </summary>
    /// <param name="luaEventName">The <see cref="ILuaCsHook.Add(string, LuaCsFunc)"/> legacy event name.</param>
    /// <param name="targetMethod">.</param>
    /// <typeparam name="T">The event interface type.</typeparam>
    /// <returns>Operation success.</returns>
    /// <exception cref="ArgumentNullException">The <see cref="luaEventName"/> is <b>null or empty.</b></exception>
    public FluentResults.Result RegisterLuaEventAlias<T>(string luaEventName, string targetMethod) where T : IEvent<T>;
}

public interface ILuaEventService : ILuaSafeEventService
{
    
}
