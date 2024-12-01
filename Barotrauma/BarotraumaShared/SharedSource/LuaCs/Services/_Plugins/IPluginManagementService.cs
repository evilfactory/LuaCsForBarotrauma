using System;
using System.Collections.Immutable;
using System.Reflection;

namespace Barotrauma.LuaCs.Services;

public interface IPluginManagementService : IService
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
}
