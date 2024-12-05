using System;
using System.Reflection;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Safe;

namespace Barotrauma.LuaCs.Services;

public interface IEventService : IReusableService, ILuaEventService
{
    /// <summary>
    /// Clears all subscribers for a given event type.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    void ClearAllEventSubscribers<T>() where T : IEvent<T>;
    /// <summary>
    /// Clears all subscribers lists.
    /// </summary>
    void ClearAllSubscribers();
    /// <summary>
    /// Invokes all alive subscribers of the given event using the provided invocation factory.
    /// </summary>
    /// <param name="action"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    FluentResults.Result PublishEvent<T>(Action<T> action) where T : IEvent<T>;
}
