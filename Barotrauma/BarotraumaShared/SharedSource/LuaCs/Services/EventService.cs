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
    void ILuaCsHook.Add(string methodId, string identifier, LuaCsFunc callback, ACsMod mod = null)
    {
        Add(methodId, identifier, callback);
    }
    [Obsolete("Use ILuaEventService.Add() instead.")]
    void ILuaCsHook.Add(string methodId, LuaCsFunc callback, ACsMod mod = null)
    {
        Add(methodId, callback);
    }
    [Obsolete("Use ILuaEventService.RemoveAll() instead.")]
    void ILuaCsHook.Remove(string methodId, string identifier)
    {
        RemoveAll(methodId, identifier);
    }

    #endregion

    #region DynamicHooks

    public Guid Add(string methodId, string identifier, LuaCsFunc callback)
    {
        throw new NotImplementedException();
    }

    public Guid Add(string methodId, LuaCsFunc callback)
    {
        throw new NotImplementedException();
    }

    public bool Exists(string methodId, string identifier)
    {
        throw new NotImplementedException();
    }

    public void RemoveAll(string methodId, string identifier)
    {
        throw new NotImplementedException();
    }

    public T Call<T>(string eventName, params object[] args)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region TypeEventSystem

    public void RegisterEventType<T>() where T : IEvent
    {
        throw new NotImplementedException();
    }

    public void UnregisterEventType<T>() where T : IEvent
    {
        throw new NotImplementedException();
    }

    /*
     * Note: Plugin wrapper for PublishEvent that handles the returned results. 
     */
    public void PublishPluginEvent<T>(Action<T> eventInvoker) where T : IEvent
    {
        _loggerService.LogResults(this.PublishEvent(eventInvoker));
    }

    public void ClearAllEventSubscribers<T>() where T : IEvent
    {
        throw new NotImplementedException();
    }

    public void ClearAllSubscribers()
    {
        throw new NotImplementedException();
    }

    public void Subscribe<T>(T observer) where T : class, IEvent
    {
        throw new NotImplementedException();
    }

    public void SubscribeAllCompat<T>(T observer) where T : class, IEvent
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
