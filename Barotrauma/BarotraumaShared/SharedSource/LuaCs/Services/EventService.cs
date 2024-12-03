using System;
using System.Reflection;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Safe;

namespace Barotrauma.LuaCs.Services;

public class EventService : IEventService
{
    #region PublicAPI

    #region Compatibility

    [Obsolete("Use ILuaEventService.Add() instead.")]
    void ILuaCsHook.Add(string eventName, string identifier, LuaCsFunc callback, ACsMod mod = null)
    {
        Add(eventName, identifier, callback);
    }
    [Obsolete("Use ILuaEventService.Add() instead.")]
    void ILuaCsHook.Add(string eventName, LuaCsFunc callback, ACsMod mod = null)
    {
        Add(eventName, callback);
    }

    #endregion

    #region DynamicHooks

    public void Add(string eventName, string identifier, LuaCsFunc callback)
    {
        throw new NotImplementedException();
    }

    public void Add(string eventName, LuaCsFunc callback)
    {
        throw new NotImplementedException();
    }

    public bool Exists(string eventName, string identifier)
    {
        throw new NotImplementedException();
    }

    public void Remove(string eventName, string identifier)
    {
        throw new NotImplementedException();
    }

    public T Call<T>(string eventName, params object[] args)
    {
        throw new NotImplementedException();
    }

    public void Call(string eventName, params object[] args)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region TypeEventSystem

    public void ClearAllEventSubscribers<T>() where T : IEvent
    {
        throw new NotImplementedException();
    }

    public void ClearAllSubscribers()
    {
        throw new NotImplementedException();
    }

    public void SubscribeAll(object observer)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result PublishEvent<T>(Action<T> eventInvoker) where T : IEvent
    {
        throw new NotImplementedException();
    }

    #endregion

    #endregion

    #region InternalAPI

    public void Dispose()
    {
        // TODO release managed resources here
        throw new NotImplementedException();
    }

    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }

    #endregion

    #region ClassFunctions

    public EventService(ILoggerService loggerService)
    {
        _loggerService = loggerService;
    }
    
    private readonly ILoggerService _loggerService;
    
    private sealed record LuaDetour
    {
        /// <summary>
        /// Unique id for the given hook.
        /// </summary>
        public Guid CallbackHookId { get; init; }
        /// <summary>
        /// Lua Function callback for the given event.
        /// </summary>
        public LuaCsFunc Callback { get; init; }
        /// <summary>
        /// String identifier given by the hook caller, if available. Not guaranteed to be unique.
        /// </summary>
        public string Identifier { get; init; }
    }

    #endregion
}
