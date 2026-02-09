#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using FluentResults;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace Barotrauma.LuaCs;

public interface ILuaScriptManagementService : IReusableService
{
    Script? InternalScript { get; }

    object? GetGlobalTableValue(string tableName);
    FluentResults.Result<DynValue> DoString(string code);
    DynValue? CallFunctionSafe(object luaFunction, params object[] args);

    /// <summary>
    /// Parses and loads script sources (code) into a memory cache without executing it.
    /// </summary>
    /// <param name="resourcesInfo"></param>
    /// <returns></returns>
    // [Required]
    Task<FluentResults.Result> LoadScriptResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourcesInfo);
    
    /// <summary>
    /// Executes already loaded into memory scripts data, in the supplied order.
    /// </summary>
    /// <param name="executionOrder"></param>
    /// <returns></returns>
    // [Required]
    FluentResults.Result ExecuteLoadedScripts(ImmutableArray<ILuaScriptResourceInfo> executionOrder, bool enableSandbox);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    // [Required]
    FluentResults.Result DisposePackageResources(ContentPackage package);
    
    /// <summary>
    /// Calls dispose on, and clears active refs for, currently running scripts. Does not clear caches.
    /// </summary>
    /// <returns></returns>
    FluentResults.Result UnloadActiveScripts();
    
    /// <summary>
    /// Unloads all scripts and clears all caches/references.
    /// </summary>
    /// <returns></returns>
    /// <remarks>May be functionally equivalent to <see cref="IReusableService.Reset"/></remarks>
    FluentResults.Result DisposeAllPackageResources();    
}
