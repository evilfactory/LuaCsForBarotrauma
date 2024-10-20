using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Barotrauma.LuaCs.Data;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace Barotrauma.LuaCs.Services;

public interface ILuaScriptService : IService
{
    #region Script_File_Runner

    /// <summary>
    /// 
    /// </summary>
    /// <param name="luaResource"></param>
    /// <returns></returns>
    bool TryAddScriptFiles(ImmutableArray<ILuaResourceInfo> luaResource);
    /// <summary>
    /// Removes the specific resources from the script runner. Important: Does not stop the
    /// execution of any code related to the files nor guarantee cleanup of resources! 
    /// </summary>
    /// <param name="luaResource"></param>
    void RemoveScriptFiles(ImmutableArray<ILuaResourceInfo> luaResource);
    ImmutableArray<ILuaResourceInfo> GetScriptResources();

    #endregion
}

public interface ILuaScriptManagementService : IService
{
    #region Type_Registration

    IUserDataDescriptor RegisterType(Type type);
    IUserDataDescriptor RegisterType(string typeName);
    IUserDataDescriptor RegisterGenericType(Type type);
    IUserDataDescriptor RegisterGenericType(string typeName, params string[] typeNameArgs);
    void UnregisterType(Type type);
    void UnregisterType(string typeName);
    void UnregisterAllTypes();
    
    #endregion
    
    #region Type_Checks_&Utilities
    
    bool IsRegistered(Type type);
    bool IsTargetType(object obj, string typeName);
    string TypeOf(object obj);
    object CreateStatic(string typeName);
    object CreateEnumTable(string typeName);
    FieldInfo FindFieldRecursively(Type type, string fieldName);
    void MakeFieldAccessible(IUserDataDescriptor descriptor, string fieldName);
    MethodInfo FindMethodRecursively(Type type, string methodName, Type[] types = null);
    void MakeMethodAccessible(IUserDataDescriptor descriptor, string methodName, string[] parameters = null);
    PropertyInfo FindPropertyRecursively(Type type, string propertyName);
    void MakePropertyAccessible(IUserDataDescriptor descriptor, string propertyName);
    void AddMethod(IUserDataDescriptor descriptor, string methodName, object function);
    void AddField(IUserDataDescriptor descriptor, string fieldName, DynValue value);
    void RemoveMember(IUserDataDescriptor descriptor, string memberName);
    bool HasMember(object obj, string memberName);
    DynValue CreateUserDataFromDescriptor(DynValue scriptObject, IUserDataDescriptor descriptor);
    DynValue CreateUserDataFromType(DynValue scriptObject, Type desiredType);
    
    #endregion
}
