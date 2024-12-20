using System;
using System.Collections.Immutable;
using System.Reflection;
using Barotrauma.LuaCs.Data;
using Microsoft.CodeAnalysis;

namespace Barotrauma.LuaCs.Services;

public interface IPluginManagementService : IReusableService
{
    /// <summary>
    /// Checks if the supplied resource is currently loaded.
    /// </summary>
    /// <param name="resource">The resource to check.</param>
    /// <returns></returns>
    bool IsResourceLoaded<T>(T resource) where T : IAssemblyResourceInfo;
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="namespacePrefix"></param>
    /// <param name="includeInterfaces"></param>
    /// <param name="includeAbstractTypes"></param>
    /// <param name="includeDefaultContext"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    FluentResults.Result<ImmutableArray<Type>> GetImplementingTypes<T>(
        string namespacePrefix = null,
        bool includeInterfaces = false,
        bool includeAbstractTypes = false,
        bool includeDefaultContext = true);
    
    /// <summary>
    /// Tries to get the
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    Type GetType(string typeName);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="resource"></param>
    /// <returns>Success/Failure and list of failed resources, if any.</returns>
    FluentResults.Result<ImmutableArray<IAssemblyResourceInfo>> LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resource);
}
