using System;

namespace Barotrauma.LuaCs.Services.Compatibility;

public interface ILuaCsHook : ILuaCsShim
{
    [Obsolete("Use ILuaEventService.Add() instead.")]
    void Add(string methodId, string identifier, LuaCsFunc callback, ACsMod mod = null);
    [Obsolete("Use ILuaEventService.Add() instead.")]
    void Add(string methodId, LuaCsFunc callback, ACsMod mod = null);
    bool Exists(string methodId, string identifier);
    [Obsolete("Use ILuaEventService.RemoveAll() instead.")]
    void Remove(string methodId, string identifier);
    T Call<T>(string eventName, params object[] args);
    void Call(string eventName, params object[] args);
}
