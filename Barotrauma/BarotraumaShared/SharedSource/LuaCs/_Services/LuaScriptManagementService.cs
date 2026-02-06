#nullable enable

using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Compatibility;
using Barotrauma.Networking;
using FluentResults;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Toolkit.Diagnostics;
using MonoMod.RuntimeDetour;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Loaders;
using RestSharp.Validation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Barotrauma.LuaCs;

namespace Barotrauma.LuaCs;

class LuaScriptManagementService : ILuaScriptManagementService, ILuaDataService
{
    private Script? _script;
    private bool _isRunning;
    [MemberNotNullWhen(true, nameof(_script))]
    public bool IsRunning => _isRunning;
    private List<ILuaScriptResourceInfo> _resourcesInfo = new List<ILuaScriptResourceInfo>();

    private readonly AsyncReaderWriterLock _operationsLock = new ();

    private readonly ILuaUserDataService _userDataService;
    private readonly ISafeLuaUserDataService _safeUserDataService;

    private readonly ILuaScriptLoader _luaScriptLoader;
    private readonly ILuaScriptServicesConfig _luaScriptServicesConfig;
    private readonly ILoggerService _loggerService;
    private readonly LuaGame _luaGame;
    private readonly ILuaCsHook _luaCsHook;
    private readonly ILuaCsTimer _luaCsTimer;
    private readonly IDefaultLuaRegistrar _defaultLuaRegistrar;
    //private readonly ILuaCsNetworking _luaCsNetworking;
    //private readonly ILuaCsUtility _luaCsUtility;

    public LuaScriptManagementService(
        ILoggerService loggerService,
        ILuaScriptLoader loader,
        ILuaUserDataService userDataService,
        ISafeLuaUserDataService safeUserDataService,
        IDefaultLuaRegistrar defaultLuaRegistrar,
        ILuaScriptServicesConfig luaScriptServicesConfig,
        LuaGame luaGame,
        ILuaCsHook luaCsHook,
        //ILuaCsNetworking luaCsNetworking,
        //ILuaCsUtility luaCsUtility,
        ILuaCsTimer luaCsTimer
        )
    {
        _luaScriptLoader = loader;
        _userDataService = userDataService;
        _safeUserDataService = safeUserDataService;
        _defaultLuaRegistrar = defaultLuaRegistrar;
        _luaScriptServicesConfig = luaScriptServicesConfig;
        _loggerService = loggerService;

        _luaGame = luaGame;
        _luaCsHook = luaCsHook;
        //_luaCsNetworking = luaCsNetworking;
        //_luaCsUtility = luaCsUtility;
        _luaCsTimer = luaCsTimer;
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

    public FluentResults.Result<DynValue> DoString(string code)
    {
        IService.CheckDisposed(this);
        if (_script == null || !IsRunning) { throw new Exception("Disposed"); }

        try
        {
            var result = _script.DoString(code);
            return FluentResults.Result.Ok(result);
        }
        catch (Exception ex)
        {
            return FluentResults.Result.Fail(new ExceptionalError(ex));
        }
    }

    private DynValue DoFile(string file, Table? globalContext = null, string? codeStringFriendly = null)
    {
        if (_script == null)
        {
            throw new Exception("Not running");
        }

        if (!LuaCsFile.CanReadFromPath(file))
        {
            throw new ScriptRuntimeException($"dofile: File access to {file} not allowed.");
        }

        if (!LuaCsFile.Exists(file))
        {
            throw new ScriptRuntimeException($"dofile: File {file} not found.");
        }

        return _script.DoFile(file, globalContext, codeStringFriendly);
    }

    private DynValue LoadFile(string file, Table? globalContext = null, string? codeStringFriendly = null)
    {
        if (_script == null)
        {
            throw new Exception("Not running");
        }

        if (!LuaCsFile.CanReadFromPath(file))
        {
            throw new ScriptRuntimeException($"loadfile: File access to {file} not allowed.");
        }

        if (!LuaCsFile.Exists(file))
        {
            throw new ScriptRuntimeException($"loadfile: File {file} not found.");
        }

        return _script.LoadFile(file, globalContext, codeStringFriendly);
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

        UserData.RegisterType(typeof(LuaGame));
        UserData.RegisterType(typeof(EventService));
        UserData.RegisterType(typeof(ILuaCsNetworking));
        UserData.RegisterType(typeof(ILuaCsUtility));
        UserData.RegisterType(typeof(ILuaCsTimer));
        UserData.RegisterType(typeof(LuaCsFile));
        UserData.RegisterType(typeof(ILuaScriptResourceInfo));
        UserData.RegisterType(typeof(IResourceInfo));
        UserData.RegisterType(typeof(IUserDataDescriptor));

        new LuaConverters(_script).RegisterLuaConverters();

        var luaRequire = new LuaRequire(_script);

        _script.Globals["setmodulepaths"] = (string[] str) => ((LuaScriptLoader)_luaScriptLoader).ModulePaths = str;

        _script.Globals["dofile"] = (Func<string, Table, string, DynValue>)DoFile;
        _script.Globals["loadfile"] = (Func<string, Table, string, DynValue>)LoadFile;
        _script.Globals["require"] = (Func<string, Table, DynValue>)luaRequire.Require;

        _script.Globals["printerror"] = (DynValue o) => { LuaCsLogger.LogError(o.ToString()); };

        _script.Globals["dostring"] = (Func<string, Table, string, DynValue>)_script.DoString;
        _script.Globals["load"] = (Func<string, Table, string, DynValue>)_script.LoadString;
        _script.Globals["Game"] = _luaGame;
        _script.Globals["Hook"] = _luaCsHook;
        _script.Globals["Timer"] = _luaCsTimer;
        _script.Globals["File"] = UserData.CreateStatic<LuaCsFile>();
        //_script.Globals["Networking"] = _luaCsNetworking;
        //_script.Globals["Steam"] = Steam;

        if (GameMain.LuaCs.IsCsEnabled)
        {
            UserData.RegisterType(typeof(LuaUserDataService));
            _script.Globals["LuaUserData"] = _userDataService;
        }
        else
        {
            UserData.RegisterType(typeof(SafeLuaUserDataService));
            _script.Globals["LuaUserData"] = _safeUserDataService;
        }

        _script.Globals["ExecutionNumber"] = 0;
        _script.Globals["CSActive"] = false;

        _script.Globals["SERVER"] = LuaCsSetup.IsServer;
        _script.Globals["CLIENT"] = LuaCsSetup.IsClient;

        _defaultLuaRegistrar.RegisterAll();
    }

    public FluentResults.Result ExecuteLoadedScripts(ImmutableArray<ILuaScriptResourceInfo> executionOrder)
    {        
        if (_isRunning) 
        { 
            return FluentResults.Result.Fail("Tried to execute Lua scripts without unloading first."); 
        }

        _loggerService.LogMessage("Executing Lua scripts");

        SetupEnvironment();

        var result = FluentResults.Result.Ok();

        _isRunning = true;

        foreach (ILuaScriptResourceInfo resource in executionOrder.Where(l => l.IsAutorun))
        {
            foreach (ContentPath filePath in resource.FilePaths)
            {
                try
                {
                    _loggerService.LogMessage($"Run {filePath.Value}");
                    _script?.Call(_script.LoadFile(filePath.FullPath), Path.GetDirectoryName(resource.OwnerPackage.Path));
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
        _userDataService.Reset();
        return DisposeAllPackageResources();
    }

    public void Dispose()
    {
        _userDataService.Dispose();
        _luaScriptLoader.Dispose();
        IsDisposed = true;
    }

    public object? GetGlobalTableValue(string tableName)
    {
        if (!IsRunning) { return null; }

        return _script.Globals[tableName];
    }
}
