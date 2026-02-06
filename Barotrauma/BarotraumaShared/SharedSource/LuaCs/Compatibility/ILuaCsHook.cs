using System;
using System.Reflection;
using Barotrauma.LuaCs;

namespace Barotrauma.LuaCs.Compatibility;

public interface ILuaCsHook : ILuaPatcher, ILuaCsShim
{
    // Event Services
    [Obsolete("ACsMod is deprecated. Use ILuaEventService.Add() instead.")]
    void Add(string eventName, string identifier, LuaCsFunc callback);
    [Obsolete("ACsMod is deprecated. Use ILuaEventService.Add() instead.")]
    void Add(string eventName, LuaCsFunc callback);
    // Does anyone use this? TODO: Analyze old Lua mods for usage scenarios.
    //bool Exists(string eventName, string identifier);
    object Call(string eventName, params object[] args);
    T Call<T>(string eventName, params object[] args);

    // Needs to be here instead of ILuaPatcher for compatiility purposes
    public enum HookMethodType
    {
        Before, After
    }
}
