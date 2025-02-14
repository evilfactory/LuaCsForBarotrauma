using System;
using System.Collections.Generic;
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
    /// Tries to get the Type given the fully qualified name.
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    Type GetType(string typeName);

    /// <summary>
    /// Loads the provided assembly resources in the order of their dependencies and intra-mod priority load order.
    /// </summary>
    /// <param name="resource"></param>
    /// <returns>Success/Failure and list of failed resources, if any.</returns>
    FluentResults.Result<ImmutableArray<IAssemblyResourceInfo>> LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resource);

    /// <summary>
    /// Creates instances of the given type and provides Property Injection and instance reference caching. Disposes of
    /// all references that throw errors on 
    /// </summary>
    /// <param name="types">List of Types</param>
    /// <param name="serviceInjection"></param>
    /// <param name="hostInstanceReference"></param>
    /// <returns></returns>
    IReadOnlyList<FluentResults.Result<(Type, T)>> ActivateTypeInstances<T>(ImmutableArray<Type> types, bool serviceInjection = true,
        bool hostInstanceReference = false) where T : IDisposable;
    
    FluentResults.Result UnloadHostedReferences();

    /// <summary>
    /// Tries to gracefully unload all hosted plugin references 
    /// </summary>
    /// <returns></returns>
    FluentResults.Result UnloadAllAssemblyResources();
}
