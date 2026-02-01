using Barotrauma;
using Barotrauma.LuaCs.Services;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Barotrauma.LuaCs.Services;

public interface ISafeLuaUserDataService : IService
{
    bool IsAllowed(string typeName);
    IUserDataDescriptor RegisterType(string typeName);
    void RegisterExtensionType(string typeName);
    bool IsRegistered(string typeName);
    void UnregisterType(string typeName, bool deleteHistory = false);
    object CreateStatic(string typeName);
    bool IsTargetType(object obj, string typeName);
    string TypeOf(object obj);
    object CreateEnumTable(string typeName);
    void MakeFieldAccessible(IUserDataDescriptor IUUD, string fieldName);
    void MakeMethodAccessible(IUserDataDescriptor IUUD, string methodName, string[] parameters = null);
    void MakePropertyAccessible(IUserDataDescriptor IUUD, string propertyName);
    void AddMethod(IUserDataDescriptor IUUD, string methodName, object function);
    void AddField(IUserDataDescriptor IUUD, string fieldName, DynValue value);
    void RemoveMember(IUserDataDescriptor IUUD, string memberName);
    bool HasMember(object obj, string memberName);
    /// <summary>
    /// See <see cref="CreateUserDataFromType"/>.
    /// </summary>
    /// <param name="scriptObject">Lua value to convert and wrap in a userdata.</param>
    /// <param name="desiredTypeDescriptor">Descriptor of the type of the object to convert the Lua value to. Uses MoonSharp ScriptToClr converters.</param>
    /// <returns>A userdata that wraps the Lua value converted to an object of the desired type as described by <paramref name="desiredTypeDescriptor"/>.</returns>
    DynValue CreateUserDataFromDescriptor(DynValue scriptObject, IUserDataDescriptor desiredTypeDescriptor);

    /// <summary>
    /// Converts a Lua value to a CLR object of a desired type and wraps it in a userdata.
    /// If the type is not registered, then a new <see cref="MoonSharp.Interpreter.Interop.StandardUserDataDescriptor"/> will be created and used.
    /// The goal of this method is to allow Lua scripts to create userdata to wrap certain data without having to register types.
    /// <remarks>Wrapping the value in a userdata preserves the original type during script-to-CLR conversions.</remarks>
    /// <example>A Lua script needs to pass a List`1 to a CLR method expecting System.Object, MoonSharp gets
    /// in the way by converting the List`1 to a MoonSharp.Interpreter.Table and breaking everything.
    /// Registering the List`1 type can break other scripts relying on default converters, so instead
    /// it is better to manually wrap the List`1 object into a userdata.
    /// </example>
    /// </summary>
    /// <param name="scriptObject">Lua value to convert and wrap in a userdata.</param>
    /// <param name="desiredType">Type describing the CLR type of the object to convert the Lua value to.</param>
    /// <returns>A userdata that wraps the Lua value converted to an object of the desired type.</returns>
    DynValue CreateUserDataFromType(DynValue scriptObject, Type desiredType);
    void AddCallMetaTable(object userdata);
}

public class SafeLuaUserDataService : ISafeLuaUserDataService
{
    private readonly ILuaUserDataService _userDataService;

    public bool IsDisposed { get; private set; }

    public SafeLuaUserDataService(ILuaUserDataService userDataService)
    {
        _userDataService = userDataService;
    }

    public IUserDataDescriptor this[string key]
    {
        get
        {
            return _userDataService.Descriptors.GetValueOrDefault(key);
        }
    }

    private bool CanBeRegistered(string typeName)
    {
        if (typeName.StartsWith("Barotrauma.Lua", StringComparison.Ordinal) ||
            typeName.StartsWith("Barotrauma.Cs", StringComparison.Ordinal) ||
            typeName.StartsWith("Barotrauma.LuaCs", StringComparison.Ordinal))
        {
            return false;
        }

        if (typeName == "System.Single") { return true; }

        if (typeName.StartsWith("System.Collections", StringComparison.Ordinal))
            return true;

        if (typeName.StartsWith("Microsoft.Xna", StringComparison.Ordinal))
            return true;

        if (typeName.StartsWith("Barotrauma.IO", StringComparison.Ordinal))
            return false;

        if (typeName.StartsWith("Barotrauma.ToolBox", StringComparison.Ordinal))
            return false;

        if (typeName.StartsWith("Barotrauma.SaveUtil", StringComparison.Ordinal))
            return false;

        if (typeName.StartsWith("Barotrauma.", StringComparison.Ordinal))
            return true;

        return false;
    }

    private bool CanBeReRegistered(string typeName)
    {
        if (typeName.StartsWith("Barotrauma.Lua", StringComparison.Ordinal) ||
            typeName.StartsWith("Barotrauma.Cs", StringComparison.Ordinal) ||
            typeName.StartsWith("Barotrauma.LuaCs", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public bool IsAllowed(string typeName)
    {
        if (!CanBeReRegistered(typeName) && IsRegistered(typeName))
        {
            return false;
        }

        if (!CanBeRegistered(typeName))
        {
            return false;
        }

        return true;
    }

    private void CheckAllowed(string typeName)
    {
        if (!IsAllowed(typeName))
        {
            throw new ScriptRuntimeException($"Type {typeName} can't be registered");
        }
    }

    public IUserDataDescriptor RegisterType(string typeName)
    {
        CheckAllowed(typeName);
        return _userDataService.RegisterType(typeName);
    }

    public void RegisterExtensionType(string typeName)
    {
        CheckAllowed(typeName);
        _userDataService.RegisterExtensionType(typeName);
    }

    public bool IsRegistered(string typeName)
    {
        return _userDataService.IsRegistered(typeName);
    }

    public void UnregisterType(string typeName, bool deleteHistory = false)
    {
        IsAllowed(typeName);
        _userDataService.UnregisterType(typeName, deleteHistory);
    }
    public object CreateStatic(string typeName)
    {
        return _userDataService.CreateStatic(typeName);
    }

    public bool IsTargetType(object obj, string typeName)
    {
        return _userDataService.IsTargetType(obj, typeName);
    }

    public string TypeOf(object obj)
    {
        return _userDataService.TypeOf(obj);
    }

    public object CreateEnumTable(string typeName)
    {
        return _userDataService.CreateEnumTable(typeName);
    }

    public void MakeFieldAccessible(IUserDataDescriptor IUUD, string fieldName)
    {
        _userDataService.MakeFieldAccessible(IUUD, fieldName);
    }

    public void MakeMethodAccessible(IUserDataDescriptor IUUD, string methodName, string[] parameters = null)
    {
        _userDataService.MakeMethodAccessible(IUUD, methodName, parameters);
    }

    public void MakePropertyAccessible(IUserDataDescriptor IUUD, string propertyName)
    {
        _userDataService.MakePropertyAccessible(IUUD, propertyName);
    }

    public void AddMethod(IUserDataDescriptor IUUD, string methodName, object function)
    {
        _userDataService.AddMethod(IUUD, methodName, function);
    }

    public void AddField(IUserDataDescriptor IUUD, string fieldName, DynValue value)
    {
        _userDataService.AddField(IUUD, fieldName, value);
    }

    public void RemoveMember(IUserDataDescriptor IUUD, string memberName)
    {
        _userDataService.RemoveMember(IUUD, memberName);
    }

    public bool HasMember(object obj, string memberName)
    {
        return _userDataService.HasMember(obj, memberName);
    }

    public DynValue CreateUserDataFromDescriptor(DynValue scriptObject, IUserDataDescriptor desiredTypeDescriptor)
    {
        return _userDataService.CreateUserDataFromDescriptor(scriptObject, desiredTypeDescriptor);
    }

    public DynValue CreateUserDataFromType(DynValue scriptObject, Type desiredType)
    {
        return _userDataService.CreateUserDataFromType(scriptObject, desiredType);
    }

    public void AddCallMetaTable(object userdata) { }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
