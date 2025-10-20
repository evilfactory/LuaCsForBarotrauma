using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using FluentResults;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace Barotrauma.LuaCs.Services;

public interface ILuaScriptManagementService : IReusableService
{
    #region Script_Ops

    Result<object> GetGlobalTableValue(string tableName);
    
    /// <summary>
    /// Parses and loads script sources (code) into a memory cache without executing it.
    /// </summary>
    /// <param name="resourcesInfo"></param>
    /// <returns></returns>
    // [Required]
    Task<FluentResults.Result> LoadScriptResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourcesInfo);
    
    /// <summary>
    /// Executes cached scripts (code) for the given <see cref="ContentPackage"/>.
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    // [Required]
    FluentResults.Result ExecuteLoadedScriptsForPackage(ContentPackage package);
    
    /// <summary>
    /// Executes cached scripts (code) for the given collection <see cref="ContentPackage"/>.
    /// </summary>
    /// <param name="packages"></param>
    /// <returns></returns>
    // [Required]
    FluentResults.Result ExecuteLoadedScriptsForPackages(IEnumerable<ContentPackage> packages);
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    // [Required]
    FluentResults.Result ExecuteLoadedScripts();
    
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

    #endregion
    
    #region Type_Registration

    IUserDataDescriptor RegisterType(Type type);
    /// <summary>
    /// <b>[Deprecated]</b><br/>
    /// Use <see cref="GetTypeInfo"/>() instead.
    /// Gets the type information for an already registered type.
    /// </summary>
    /// <param name="typeName">The fully qualified name of the type and namespace.</param>
    /// <returns>The <see cref="IUserDataDescriptor"/> for the type, if registered. Null if none is found.</returns>
    [Obsolete($"Use {nameof(GetTypeInfo)} instead.")]
    IUserDataDescriptor RegisterType(string typeName) => GetTypeInfo(typeName);
    IUserDataDescriptor RegisterGenericType(Type type);
    /// <summary>
    /// <b>[Deprecated]</b><br/>
    /// Use <see cref="GetTypeInfo"/>() instead.
    /// Gets the generic type information for an already registered type.
    /// </summary>
    /// <param name="typeName">The fully qualified name of the generic type and namespace.</param>
    /// <param name="typeNameArgs">The fully qualified name of the template types.</param>
    /// <returns>The <see cref="IUserDataDescriptor"/> for the type, if registered. Null if none is found.</returns>
    [Obsolete($"Use {nameof(GetGenericTypeInfo)} instead.")]
    IUserDataDescriptor RegisterGenericType(string typeName, params string[] typeNameArgs) => GetGenericTypeInfo(typeName, typeNameArgs);
    /// <summary>
    /// Gets the type information for an already registered type.
    /// </summary>
    /// <param name="typeName">The fully qualified name of the type and namespace.</param>
    /// <returns>The <see cref="IUserDataDescriptor"/> for the type, if registered. Null if none is found.</returns>
    IUserDataDescriptor GetTypeInfo(string typeName);
    /// <summary>
    /// Gets the generic type information for an already registered type.
    /// </summary>
    /// <param name="typeName">The fully qualified name of the generic type and namespace.</param>
    /// <param name="typeNameArgs">The fully qualified name of the template types.</param>
    /// <returns>The <see cref="IUserDataDescriptor"/> for the type, if registered. Null if none is found.</returns>
    IUserDataDescriptor GetGenericTypeInfo(string typeName, params string[] typeNameArgs);
    void UnregisterType(Type type);
    
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
