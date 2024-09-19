using System;
using System.Reflection;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace Barotrauma.LuaCs.Services;

public interface ILuaScriptService
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
    
    #region Script_File_Runner

    void AddScriptFiles(string[] filePaths);
    void RemoveScriptFiles(string[] filePaths);
    void RunLoadedScripts();

    #endregion
}
