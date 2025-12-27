#nullable enable

using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Safe;
using FluentResults;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Loaders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Barotrauma.LuaCs.Services;

public class LuaScriptManagementService : ILuaScriptManagementService, ILuaDataService
{
    private Script? _script;
    private bool _isRunning;
    [MemberNotNullWhen(true, nameof(_script))]
    public bool IsRunning => _isRunning;
    private List<ILuaScriptResourceInfo> _resourcesInfo = new List<ILuaScriptResourceInfo>();
    private readonly ILuaScriptLoader _luaScriptLoader;
    private readonly ILuaScriptServicesConfig _luaScriptServicesConfig;
    private readonly ILoggerService _loggerService;

    public LuaScriptManagementService(ILoggerService loggerService, ILuaScriptLoader loader, ILuaScriptServicesConfig luaScriptServicesConfig)
    {
        _luaScriptLoader = loader;
        _luaScriptServicesConfig = luaScriptServicesConfig;
        _loggerService = loggerService;
    }

    public bool IsDisposed { get; private set; }

    public Task<FluentResults.Result> LoadScriptResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourcesInfo)
    {
        _resourcesInfo.AddRange(resourcesInfo.OrderBy(static r => r.LoadPriority));

        // TODO disk caching

        return Task.FromResult(FluentResults.Result.Ok());
    }

    private void SetupEnvironment()
    {
        _script = new Script(CoreModules.Preset_SoftSandbox | CoreModules.Debug | CoreModules.IO | CoreModules.OS_System);
        _script.Options.DebugPrint = (string msg) =>
        {
            _loggerService.Log(msg);
        };
        _script.Options.ScriptLoader = _luaScriptLoader;
        _script.Options.CheckThreadAccess = false;

        Script.GlobalOptions.ShouldPCallCatchException = (Exception ex) => { return true; };
    }

    public FluentResults.Result ExecuteLoadedScripts()
    {
        if (_isRunning) 
        { 
            return FluentResults.Result.Fail("Tried to execute Lua scripts without unloading first."); 
        }

        SetupEnvironment();

        _isRunning = true;

        var result = FluentResults.Result.Ok();

        foreach (var resource in _resourcesInfo)
        {
            foreach (var filePath in resource.FilePaths)
            {
                try
                {
                    _script?.Call(_script.LoadFile(filePath));
                }
                catch(Exception e)
                {
                    result = result.WithError(new ExceptionalError(e));
                }
            }
        }

        return result;
    }

    public DynValue? CallFunction(DynValue luaFunction, params object[] args)
    {
        if (!IsRunning) { return null; }

        lock (_script)
        {
            try
            {
                return _script.Call(luaFunction, args);
            }
            catch (Exception e)
            {
                _loggerService.HandleException(e);
            }
            return null;
        }
    }

    public FluentResults.Result UnloadActiveScripts()
    {
        _isRunning = false;

        _script = null;

        // todo unregister everything

        return FluentResults.Result.Ok();
    }

    public FluentResults.Result DisposePackageResources(ContentPackage package)
    {
        return FluentResults.Result.Fail("Not supported for Lua");
    }

    public FluentResults.Result DisposeAllPackageResources()
    {
        if (IsRunning)
        {
            UnloadActiveScripts();
        }

        _resourcesInfo.Clear();

        return FluentResults.Result.Ok();
    }

    public FluentResults.Result Reset()
    {
        return DisposeAllPackageResources();
    }

    public void Dispose()
    {
        _luaScriptLoader.Dispose();
        IsDisposed = true;
    }

    public IUserDataDescriptor RegisterType(Type type)
    {
        return UserData.RegisterType(type);
    }
    public void UnregisterType(Type type)
    {
        UserData.UnregisterType(type, true);
    }

    public object? GetGlobalTableValue(string tableName)
    {
        if (!IsRunning) { return null; }

        return _script.Globals[tableName];
    }
}
