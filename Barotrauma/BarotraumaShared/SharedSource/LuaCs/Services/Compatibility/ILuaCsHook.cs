using System;

namespace Barotrauma.LuaCs.Services.Compatibility;

public interface ILuaCsHook : ILuaCsShim
{
    [Obsolete("ACsMod is deprecated. Use ILuaEventService.Add() instead.")]
    void Add(string eventName, string identifier, LuaCsFunc callback, ACsMod mod = null);
    [Obsolete("ACsMod is deprecated. Use ILuaEventService.Add() instead.")]
    void Add(string eventName, LuaCsFunc callback, ACsMod mod = null);
    bool Exists(string eventName, string identifier);
    [Obsolete("Only Lua subscribers will receive events from call. Use ILuaEventService.Add() instead.")]
    T Call<T>(string eventName, params object[] args);
    object Call(string eventName, params object[] args);
}
