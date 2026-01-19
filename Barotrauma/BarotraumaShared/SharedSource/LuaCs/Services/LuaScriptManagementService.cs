#nullable enable

using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Safe;
using Barotrauma.Networking;
using FluentResults;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MonoMod.RuntimeDetour;
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
using Microsoft.Toolkit.Diagnostics;
using static Barotrauma.GameSettings;

namespace Barotrauma.LuaCs.Services;

class LuaScriptManagementService : ILuaScriptManagementService, ILuaDataService
{
    private Script? _script;
    private bool _isRunning;
    [MemberNotNullWhen(true, nameof(_script))]
    public bool IsRunning => _isRunning;
    private List<ILuaScriptResourceInfo> _resourcesInfo = new List<ILuaScriptResourceInfo>();

    private readonly AsyncReaderWriterLock _operationsLock = new ();
    
    private readonly ILuaScriptLoader _luaScriptLoader;
    private readonly ILuaScriptServicesConfig _luaScriptServicesConfig;
    private readonly ILoggerService _loggerService;
    private readonly LuaGame _luaGame;
    private readonly ILuaCsHook _luaCsHook;
    //private readonly ILuaCsNetworking _luaCsNetworking;
    //private readonly ILuaCsUtility _luaCsUtility;
    //private readonly ILuaCsTimer _luaCsTimer;

    public LuaScriptManagementService(
        ILoggerService loggerService, 
        ILuaScriptLoader loader, 
        ILuaScriptServicesConfig luaScriptServicesConfig,
        LuaGame luaGame,
        ILuaCsHook luaCsHook
        //ILuaCsNetworking luaCsNetworking,
        //ILuaCsUtility luaCsUtility,
        //ILuaCsTimer luaCsTimer
        )
    {
        _luaScriptLoader = loader;
        _luaScriptServicesConfig = luaScriptServicesConfig;
        _loggerService = loggerService;

        _luaGame = luaGame;
        _luaCsHook = luaCsHook;
        //_luaCsNetworking = luaCsNetworking;
        //_luaCsUtility = luaCsUtility;
        //_luaCsTimer = luaCsTimer;
    }

    public bool IsDisposed { get; private set; }

    public async Task<FluentResults.Result> LoadScriptResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourcesInfo)
    {
        // Do any exception checks you can before acquiring a lock to avoid needlessly holding up resources.
        if (resourcesInfo.IsDefaultOrEmpty)
            ThrowHelper.ThrowArgumentNullException($"{nameof(LoadScriptResourcesAsync)}: The parameter is empty!");
        
        // Acquire a lock:
        // Reader = Allow parallel operations (try to avoid nesting acquiring the lock when possible)
        // Writer = Exclusive use (ie. executing scripts or Dispose())
        using var lck = await _operationsLock.AcquireWriterLock();   // IDisposable using with generate a try-finally and release for you.
        IService.CheckDisposed(this);                                    // Check disposed after you have the lock  
        
        // If you use a ConcurrentDictionary instead of a List, it will handle threading issues for you.
        _resourcesInfo.AddRange(resourcesInfo.OrderBy(static r => r.LoadPriority));

        // Use the StorageService's caching function by just loading the file with caching turned on.
        // Right now the LuaScriptLoader has this on by default.
        var cacheRes = await _luaScriptLoader.CacheResourcesAsync(resourcesInfo);
        
        // Aggregate and return results to the caller to deal with. Optionally, log here if you want.
        // Automatically converted to a Task<T> when 'async' is in the method declaration.
        if (cacheRes.IsFailed)
            return cacheRes.ToResult();
        return new FluentResults.Result().WithReasons(cacheRes.Value.SelectMany(cr => cr.Item2.Reasons));
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

        RegisterType(typeof(LuaGame));
        RegisterType(typeof(ILuaCsHook));
        RegisterType(typeof(ILuaCsNetworking));
        RegisterType(typeof(ILuaCsUtility));
        RegisterType(typeof(ILuaCsTimer));
        RegisterType(typeof(LuaCsFile));

        new LuaConverters(_script).RegisterLuaConverters();

        _script.Globals["printerror"] = (DynValue o) => { LuaCsLogger.LogError(o.ToString()); };

        _script.Globals["dostring"] = (Func<string, Table, string, DynValue>)_script.DoString;
        _script.Globals["load"] = (Func<string, Table, string, DynValue>)_script.LoadString;

        _script.Globals["Game"] = _luaGame;
        _script.Globals["Hook"] = _luaCsHook;
        //_script.Globals["Timer"] = _luaCsTimer;
        _script.Globals["File"] = UserData.CreateStatic<LuaCsFile>();
        //_script.Globals["Networking"] = _luaCsNetworking;
        //_script.Globals["Steam"] = Steam;

        _script.Globals["ExecutionNumber"] = 0;
        _script.Globals["CSActive"] = false;

        _script.Globals["SERVER"] = LuaCsSetup.IsServer;
        _script.Globals["CLIENT"] = LuaCsSetup.IsClient;
    }

    public FluentResults.Result ExecuteLoadedScripts(ImmutableArray<ILuaScriptResourceInfo> executionOrder)
    {
        throw new NotImplementedException($"Need to implement {nameof(executionOrder)} logic.");
        
        if (_isRunning) 
        { 
            return FluentResults.Result.Fail("Tried to execute Lua scripts without unloading first."); 
        }

        SetupEnvironment();

        _isRunning = true;

        var result = FluentResults.Result.Ok();

        foreach (ILuaScriptResourceInfo resource in _resourcesInfo)
        {
            foreach (ContentPath filePath in resource.FilePaths)
            {
                try
                {
                    _script?.Call(_script.LoadFile(filePath.FullPath));
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
