using Barotrauma.LuaCs.Data;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public class LuaScriptManagementService : ILuaScriptManagementService
{
    private Script _sharedSession;
    private readonly ConcurrentDictionary<ContentPackage, StringBuilder> _cpScripts;
    private readonly IStorageService _storageService;
    
    public LuaScriptManagementService(IStorageService storageService)
    {
        _sharedSession = new Script();
        _cpScripts = new();
        this._storageService = storageService;
        _storageService.UseCaching = false;
    }
    
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public bool IsDisposed { get; private set; }
    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }

    public Result<object> GetGlobalTableValue(string tableName)
    {
        ((IService)this).CheckDisposed();
        
        if (_sharedSession == null)
        {
            return FluentResults.Result.Fail<object>($"LuaService: There is no active script session.");
        }

        if (tableName.IsNullOrWhiteSpace())
        {
            return FluentResults.Result.Fail<object>($"LuaService: Table name is invalid.");
        }

        try
        {
            var tblVal = _sharedSession.Globals[tableName];
            if (tblVal is null)
                return FluentResults.Result.Fail<object>($"LuaService: No value found for object table name {tableName}.");
            return tblVal;
        }
        catch
        {
            return FluentResults.Result.Fail<object>($"LuaService: Failed to retrieve table.");
        }
    }

    public async Task<FluentResults.Result> LoadScriptResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourcesInfo)
    {
        ((IService)this).CheckDisposed();
        
        if (resourcesInfo.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"LuaService: Script resources are empty.");
        var validRes = resourcesInfo
            .Where(r => (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0)
            .Where(r => (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0)
            .Where(r => r.SupportedCultures.Contains(CultureInfo.CurrentCulture))
            // only include autorun files, lua-require should be used by scripts.
            .Where(r => r.IsAutorun)
            .OrderByDescending(r => r.LoadPriority)
            .ToImmutableArray();
        
        foreach (var scriptResource in validRes)
        {
            if (scriptResource.FilePaths.IsDefaultOrEmpty)
                continue;
            foreach (var filePath in scriptResource.FilePaths)
            {
                if (filePath.IsNullOrWhiteSpace())
                    continue;
                if (await _storageService.TryLoadTextAsync(filePath) is not { IsSuccess: true } contents)
                    continue;
                if (contents.Value.IsNullOrWhiteSpace())
                    continue;
                _cpScripts.AddOrUpdate(scriptResource.OwnerPackage,
                    cp =>
                    {
                        var sb = new StringBuilder();
                        sb.Append(contents.Value);
                        return sb;
                    },
                    (cp, sb) =>
                    {
                        sb.Append(contents.Value);
                        return sb;
                    });
            }
        }

        return FluentResults.Result.Ok();
    }

    public FluentResults.Result ExecuteLoadedScriptsForPackage(ContentPackage package, bool pauseExecutionOnError = false,
        bool verboseLogging = false)
    {
        ((IService)this).CheckDisposed();

        if (!ModUtils.Environment.IsMainThread)
        {
            return FluentResults.Result.Fail(new ExceptionalError($"LuaService: Tried to execute scripts on worker thread.", new InvalidOperationException())
                .WithMetadata(FluentResults.LuaCs.MetadataType.ExceptionObject, this)); 
        }
        
        if (_sharedSession == null)
        {
            return FluentResults.Result.Fail(new ExceptionalError($"LuaService: No active script session.", new InvalidOperationException())
                .WithMetadata(FluentResults.LuaCs.MetadataType.ExceptionObject, this));
        }

        if (package == null)
        {
            return FluentResults.Result.Fail($"LuaService: Package is null.");
        }

        if (!_cpScripts.TryGetValue(package, out var result))
        {
            return FluentResults.Result.Fail($"LuaService: No scripts found for package {package.Name}.");
        }

        try
        {
            _sharedSession.DoString(result.ToString()); // add to runtime
        }
        catch (ScriptRuntimeException sre)
        {
            return FluentResults.Result.Fail(new ExceptionalError($"LuaService: Executed scripts for {package.Name} with exceptions.", sre)
                .WithMetadata(FluentResults.LuaCs.MetadataType.ExceptionObject, this)
                .WithMetadata(FluentResults.LuaCs.MetadataType.RootObject, package));
        }
        catch (InterpreterException ie)
        {
            return FluentResults.Result.Fail(new ExceptionalError($"LuaService: Script runtime failure while execution package {package.Name}", ie)
                .WithMetadata(FluentResults.LuaCs.MetadataType.ExceptionObject, this));
        }
        
        return FluentResults.Result.Ok();
    }

    public FluentResults.Result ExecuteLoadedScriptsForPackages(IEnumerable<ContentPackage> packages, bool pauseExecutionOnError = false,
        bool verboseLogging = false)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result LoadExecuteImmediate(ImmutableArray<ILuaScriptResourceInfo> scripts, bool pauseExecutionOnError = false, bool verboseLogging = false)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result ExecuteLoadedScripts(bool pauseExecutionOnError = false, bool verboseLogging = false)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result DisposePackageResources(ContentPackage package)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result UnloadActiveScripts()
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result DisposeAllPackageResources()
    {
        throw new NotImplementedException();
    }

    public IUserDataDescriptor RegisterType(Type type)
    {
        throw new NotImplementedException();
    }

    public IUserDataDescriptor RegisterGenericType(Type type)
    {
        throw new NotImplementedException();
    }

    public IUserDataDescriptor GetTypeInfo(string typeName)
    {
        throw new NotImplementedException();
    }

    public IUserDataDescriptor GetGenericTypeInfo(string typeName, params string[] typeNameArgs)
    {
        throw new NotImplementedException();
    }

    public void UnregisterType(Type type)
    {
        throw new NotImplementedException();
    }

    public bool IsRegistered(Type type)
    {
        throw new NotImplementedException();
    }

    public bool IsTargetType(object obj, string typeName)
    {
        throw new NotImplementedException();
    }

    public string TypeOf(object obj)
    {
        throw new NotImplementedException();
    }

    public object CreateStatic(string typeName)
    {
        throw new NotImplementedException();
    }

    public object CreateEnumTable(string typeName)
    {
        throw new NotImplementedException();
    }

    public FieldInfo FindFieldRecursively(Type type, string fieldName)
    {
        throw new NotImplementedException();
    }

    public void MakeFieldAccessible(IUserDataDescriptor descriptor, string fieldName)
    {
        throw new NotImplementedException();
    }

    public MethodInfo FindMethodRecursively(Type type, string methodName, Type[] types = null)
    {
        throw new NotImplementedException();
    }

    public void MakeMethodAccessible(IUserDataDescriptor descriptor, string methodName, string[] parameters = null)
    {
        throw new NotImplementedException();
    }

    public PropertyInfo FindPropertyRecursively(Type type, string propertyName)
    {
        throw new NotImplementedException();
    }

    public void MakePropertyAccessible(IUserDataDescriptor descriptor, string propertyName)
    {
        throw new NotImplementedException();
    }

    public void AddMethod(IUserDataDescriptor descriptor, string methodName, object function)
    {
        throw new NotImplementedException();
    }

    public void AddField(IUserDataDescriptor descriptor, string fieldName, DynValue value)
    {
        throw new NotImplementedException();
    }

    public void RemoveMember(IUserDataDescriptor descriptor, string memberName)
    {
        throw new NotImplementedException();
    }

    public bool HasMember(object obj, string memberName)
    {
        throw new NotImplementedException();
    }

    public DynValue CreateUserDataFromDescriptor(DynValue scriptObject, IUserDataDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    public DynValue CreateUserDataFromType(DynValue scriptObject, Type desiredType)
    {
        throw new NotImplementedException();
    }
}
