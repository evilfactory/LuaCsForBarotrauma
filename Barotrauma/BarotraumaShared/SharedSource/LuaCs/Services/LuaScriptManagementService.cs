using Barotrauma.LuaCs.Data;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;

namespace Barotrauma.LuaCs.Services;

public class LuaScriptManagementService : ILuaScriptManagementService
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public bool IsDisposed { get; set; }
    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> LoadScriptResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourcesInfo)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result ExecuteLoadedScriptsForPackage(ContentPackage package, bool pauseExecutionOnError = false,
        bool verboseLogging = false)
    {
        throw new NotImplementedException();
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
