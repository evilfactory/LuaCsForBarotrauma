using System;
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
