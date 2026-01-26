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
    /// Gets all  types in searched <see cref="IAssemblyLoaderService"/> that implement the type supplied.
    /// </summary>
    /// <param name="includeInterfaces"></param>
    /// <param name="includeAbstractTypes"></param>
    /// <param name="includeDefaultContext"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    FluentResults.Result<ImmutableArray<Type>> GetImplementingTypes<T>(
        bool includeInterfaces = false,
        bool includeAbstractTypes = false,
        bool includeDefaultContext = true);
    
    /// <summary>
    /// Tries to find the type given the fully qualified name and filters.
    /// </summary>
    /// <param name="typeName"></param>
    /// <param name="isByRefType"></param>
    /// <param name="includeInterfaces"></param>
    /// <param name="includeDefaultContext"></param>
    /// <returns></returns>
    Type GetType(string typeName, bool isByRefType = false, bool includeInterfaces = false, bool includeDefaultContext = true);

    /// <summary>
    /// Loads the provided assembly resources in the order of their dependencies and intra-mod priority load order.
    /// </summary>
    /// <param name="resources"></param>
    /// <returns>Success/Failure and list of failed resources, if any.</returns>
    FluentResults.Result LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resources);

    /// <summary>
    /// Creates instances of the given type and provides Property Injection and instance reference caching. Disposes of
    /// all references that throw errors on 
    /// </summary>
    /// <param name="types">List of Types</param>
    /// <param name="serviceInjection"></param>
    /// <param name="hostInstanceReference"></param>
    /// <returns></returns>
    ImmutableArray<FluentResults.Result<(Type, T)>> ActivateTypeInstances<T>(ImmutableArray<Type> types, bool serviceInjection = true,
        bool hostInstanceReference = false) where T : IDisposable;
    
    
    /// <summary>
    /// Unloads all managed <see cref="IAssemblyPlugin"/>, <see cref="Assembly"/>, and <see cref="IAssemblyLoaderService"/>s.
    /// </summary>
    /// <returns>Success of the operation. <br/><b>Note: does not guarantee .NET runtime assembly unloading success.</b></returns>
    FluentResults.Result UnloadManagedAssemblies();
}
