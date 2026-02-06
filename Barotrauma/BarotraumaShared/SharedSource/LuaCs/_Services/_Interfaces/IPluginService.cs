using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs;

public interface IPluginService : IReusableService
{
    bool IsAssemblyLoaded(string friendlyName);
    /// <summary>
    /// Loads the assemblies for the given information
    /// </summary>
    /// <param name="assemblyResourcesInfo"></param>
    /// <param name="injectServices"></param>
    /// <param name="typeInstances"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    FluentResults.Result LoadAndInstanceTypes<T>(IEnumerable<IAssemblyResourceInfo> assemblyResourcesInfo, bool injectServices, out ImmutableArray<T> typeInstances) where T : class, IAssemblyPlugin;
    FluentResults.Result<ImmutableArray<T>> GetLoadedPluginTypesInPackage<T>() where T : class, IAssemblyPlugin;
    /// <summary>
    /// Advances the loading/execution state of the plugin. IMPORTANT: You cannot set the execution state of plugins
    /// to 'Disposed'. You must instead call the 'DisposePlugins' method. 
    /// </summary>
    /// <param name="newState"></param>
    /// <returns></returns>
    FluentResults.Result AdvancePluginStates(PluginRunState newState);

    /// <summary>
    /// Disposes of all running plugins hosted by the service and releases their references to allow unloading.
    /// </summary>
    /// <returns>Success of the operation. Returns false if any plugin threw errors during disposal.</returns>
    FluentResults.Result DisposePlugins();

    /// <summary>
    /// Gets the current plugin execution state.
    /// </summary>
    /// <returns></returns>
    PluginRunState GetPluginRunState();
}

public enum PluginRunState
{
    Instanced=0, 
    PreInitialization=1, 
    Initialized=2, 
    LoadingCompleted=3, 
    Disposed=4
}
