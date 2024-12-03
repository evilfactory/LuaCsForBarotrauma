using System;
using System.Reflection;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Safe;

namespace Barotrauma.LuaCs.Services;

public interface IEventService : IReusableService, ILuaEventService, IPluginEventService
{
    /// <summary>
    /// Clears all subscribers for a given event type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    void ClearAllEventSubscribers<T>() where T : IEvent;
    /// <summary>
    /// Clears all subscribers lists.
    /// </summary>
    void ClearAllSubscribers();
    /// <summary>
    /// Subscribes instance to events of the given type.
    /// Note: The event system holds WeakRef to the type and requires
    /// that instance refs be maintained elsewhere.
    /// </summary>
    /// <param name="observer"></param>
    /// <typeparam name="T"></typeparam>
    void Subscribe<T>(T observer) where T : class, IEvent;
    /// <summary>
    /// Subscribes instance to all registered events the given type implements.
    /// Note: The event system holds WeakRef to the type and requires
    /// that instance refs be maintained elsewhere.
    /// </summary>
    /// <param name="observer"></param>
    /// <typeparam name="T"></typeparam>
    void SubscribeAllCompat<T>(T observer) where T : class, IEvent;
    /// <summary>
    /// Invokes all alive subscribers of the given event using the provided
    /// invocation factory.
    /// </summary>
    /// <param name="eventInvoker"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    FluentResults.Result PublishEvent<T>(Action<T> eventInvoker) where T : IEvent;
}

public interface IPluginEventService
{
    /// <summary>
    /// Register an event type for assembly scanning and invocation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    void RegisterEventType<T>() where T : IEvent;
    /// <summary>
    /// Unregister type from event system and clears all subscriber
    /// references to the type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    void UnregisterEventType<T>() where T : IEvent;
    /// <summary>
    /// Invokes all alive subscribers of the given event using the provided
    /// invocation factory.
    /// </summary>
    /// <param name="eventInvoker"></param>
    /// <typeparam name="T"></typeparam>
    void PublishPluginEvent<T>(Action<T> eventInvoker) where T : IEvent;
}
