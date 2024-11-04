using System.Collections.Immutable;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.CodeAnalysis;

namespace Barotrauma.LuaCs.Services;

public class PluginManagementService : IPluginManagementService 
{
    
    
    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public FluentResults.Result Reset()
    {
        throw new System.NotImplementedException();
    }

    public bool IsAssemblyLoadedGlobal(string friendlyName)
    {
        throw new System.NotImplementedException();
    }

    public Result<ImmutableArray<T>> GetTypes<T>(ContentPackage package = null, string namespacePrefix = null, bool includeInterfaces = false,
        bool includeAbstractTypes = false, bool includeDefaultContext = true, bool includeExplicitAssembliesOnly = false)
    {
        throw new System.NotImplementedException();
    }

    public ImmutableArray<MetadataReference> GetStandardMetadataReferences()
    {
        throw new System.NotImplementedException();
    }

    public ImmutableArray<MetadataReference> GetPluginMetadataReferences()
    {
        throw new System.NotImplementedException();
    }

    public Result<ImmutableArray<IAssemblyResourceInfo>> GetCachedAssembliesForPackage(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }

    public Result<ImmutableArray<IAssemblyResourceInfo>> LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resource)
    {
        throw new System.NotImplementedException();
    }
}
