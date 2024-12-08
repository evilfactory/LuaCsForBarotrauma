using System;
using System.Reflection;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Safe;

namespace Barotrauma.LuaCs.Services;

public interface IEventService : IReusableService, ILuaEventService
{
    FluentResults.Result SetLegacyLuaRunnerFactory<T>(Func<LuaCsFunc, T> runnerFactory) where T : IEvent<T>;
    void RemoveLegacyLuaRunnerFactory<T>() where T : IEvent<T>;
    void SetAliasToEvent<T>(string alias) where T : IEvent<T>;
    void RemoveEventAlias(string alias);
    void RemoveAllEventAliases<T>() where T : IEvent<T>;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="subscriber"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    FluentResults.Result Subscribe<T>(T subscriber) where T : IEvent<T>;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="subscriber"></param>
    /// <typeparam name="T"></typeparam>
    void Unsubscribe<T>(T subscriber) where T : IEvent;
    /// <summary>
    /// Clears all subscribers for a given event type and removes any registration to the type.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    void ClearAllEventSubscribers<T>() where T : IEvent;
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
