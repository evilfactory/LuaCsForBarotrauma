using System;

namespace Barotrauma.LuaCs.Services.Compatibility;

public interface ILuaCsHook : ILuaCsShim
{
    [Obsolete("Use ILuaEventService.Add() instead.")]
    void Add(string eventName, string identifier, LuaCsFunc callback, ACsMod mod = null);
    [Obsolete("Use ILuaEventService.Add() instead.")]
    void Add(string eventName, LuaCsFunc callback, ACsMod mod = null);
    bool Exists(string eventName, string identifier);
    T Call<T>(string eventName, params object[] args);
    void Call(string eventName, params object[] args);
}
