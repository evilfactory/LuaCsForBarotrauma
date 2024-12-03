using System;
using System.Collections.Immutable;
using System.Reflection;
using Barotrauma.LuaCs.Data;
using Microsoft.CodeAnalysis;

namespace Barotrauma.LuaCs.Services;

public interface IPluginManagementService : IReusableService
{
    /// <summary>
    /// Checks if an assembly with either the fully-qualified name globally or a 'friendly name' within loaded plugins
    /// with the given name is loaded. 
    /// </summary>
    /// <param name="friendlyName"></param>
    /// <returns></returns>
    bool IsAssemblyLoadedGlobal(string friendlyName);
    
    // TODO: Documentation.
    FluentResults.Result<ImmutableArray<T>> GetTypes<T>(
        ContentPackage package = null,
        string namespacePrefix = null,
        bool includeInterfaces = false,
        bool includeAbstractTypes = false,
        bool includeDefaultContext = true,
        bool includeExplicitAssembliesOnly = false);
    
    /// <summary>
    /// Gets the assembly <c>MetadataReference</c> collection for the BCL and base game assemblies. 
    /// </summary>
    /// <returns></returns>
    ImmutableArray<MetadataReference> GetStandardMetadataReferences();
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    ImmutableArray<MetadataReference> GetPluginMetadataReferences();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    FluentResults.Result<ImmutableArray<IAssemblyResourceInfo>> GetCachedAssembliesForPackage(ContentPackage package);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="resource"></param>
    /// <returns>Success/Failure and list of failed resources, if any.</returns>
    FluentResults.Result<ImmutableArray<IAssemblyResourceInfo>> LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resource);
}
