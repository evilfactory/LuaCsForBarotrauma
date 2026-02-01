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
    /// 
    /// </summary>
    /// <param name="executionOrder"></param>
    /// <param name="excludeAlreadyRunningPackages"></param>
    /// <returns></returns>
    FluentResults.Result ActivatePluginInstances(ImmutableArray<ContentPackage> executionOrder, bool excludeAlreadyRunningPackages = true);
    
    /// <summary>
    /// Loads the provided assembly resources in the order of their dependencies and intra-mod priority load order.
    /// </summary>
    /// <param name="resources"></param>
    /// <returns>Success/Failure and list of failed resources, if any.</returns>
    FluentResults.Result LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resources);
    
    /// <summary>
    /// Unloads all managed <see cref="IAssemblyPlugin"/>, <see cref="Assembly"/>, and <see cref="IAssemblyLoaderService"/>s.
    /// </summary>
    /// <returns>Success of the operation. <br/><b>Note: does not guarantee .NET runtime assembly unloading success.</b></returns>
    FluentResults.Result UnloadManagedAssemblies();
}
