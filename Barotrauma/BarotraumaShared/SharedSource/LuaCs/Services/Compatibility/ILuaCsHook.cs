using System;

namespace Barotrauma.LuaCs.Services.Compatibility;

public interface ILuaCsHook : ILuaCsShim
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
    
    // Hook/Method Patching
    string Patch(string identifier, string className, string methodName, string[] parameterTypes, LuaCsPatchFunc patch, EventService.HookMethodType hookType = EventService.HookMethodType.Before);
    string Patch(string identifier, string className, string methodName, LuaCsPatchFunc patch, EventService.HookMethodType hookType = EventService.HookMethodType.Before);
    string Patch(string className, string methodName, string[] parameterTypes, LuaCsPatchFunc patch, EventService.HookMethodType hookType = EventService.HookMethodType.Before);
    string Patch(string className, string methodName, LuaCsPatchFunc patch, EventService.HookMethodType hookType = EventService.HookMethodType.Before);
    bool RemovePatch(string identifier, string className, string methodName, string[] parameterTypes, EventService.HookMethodType hookType);
    bool RemovePatch(string identifier, string className, string methodName, EventService.HookMethodType hookType);
}
