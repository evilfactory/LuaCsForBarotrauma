using System;
using System.Reflection;
using LuaCsCompatPatchFunc = Barotrauma.LuaCsPatch;

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
    string Patch(string identifier, string className, string methodName, string[] parameterTypes, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before);
    string Patch(string identifier, string className, string methodName, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before);
    string Patch(string className, string methodName, string[] parameterTypes, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before);
    string Patch(string className, string methodName, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before);
    bool RemovePatch(string identifier, string className, string methodName, string[] parameterTypes, HookMethodType hookType);
    bool RemovePatch(string identifier, string className, string methodName, HookMethodType hookType);

    void HookMethod(string identifier, MethodBase method, LuaCsCompatPatchFunc patch, HookMethodType hookType = HookMethodType.Before, IAssemblyPlugin owner = null);
    
    
    public enum HookMethodType
    {
        Before, After
    }
}
