using System;
using System.Collections.Generic;
using System.Reflection;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;
using Dynamitey;
using ImpromptuInterface;

namespace Barotrauma.LuaCs.Events;

/*
 * The following is a collection of interfaces that types can implement to be registered events.
 * Note: Internally-marked interfaces should be consumed using a publicizer. This is due to the Barotrauma source
 * types being internal by default.
*/

public interface IEvent
{
    bool IsLuaRunner() => false;
}

public interface IEvent<out T> : IEvent where T : IEvent<T>
{
    static virtual T GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
    {
        throw new InvalidOperationException($"Lua runners forbidden for  {typeof(T).Name}");
    }
}

#region RuntimeServiceEvents

/// <summary>
/// Called when the current <see cref="Screen"/> (game state) changes. Upstream Type 'Screen' is internal. 
/// </summary>
internal interface IEventScreenSelected : IEvent<IEventScreenSelected>
{
    void OnScreenSelected(Screen screen);
}

/// <summary>
/// Called whenever the list of all <see cref="ContentPackage"/> (enabled and disabled) on disk has changed.
/// </summary>
internal interface IEventAllPackageListChanged : IEvent<IEventAllPackageListChanged>
{
    void OnAllPackageListChanged(IEnumerable<CorePackage> corePackages, IEnumerable<RegularPackage> regularPackages);
}

/// <summary>
/// Called whenever the list of enabled <see cref="ContentPackage"/> has changed.
/// </summary>
internal interface IEventEnabledPackageListChanged : IEvent<IEventEnabledPackageListChanged>
{
    void OnEnabledPackageListChanged(CorePackage package, IEnumerable<RegularPackage> regularPackages);
}

internal interface IEventReloadAllPackages : IEvent<IEventReloadAllPackages>
{
    void OnReloadAllPackages();
}

internal interface IEventSettingInstanceLifetime : IEvent<IEventSettingInstanceLifetime>
{
    void OnSettingInstanceCreated<T>(T configInstance) where T : ISettingBase;
    void OnSettingInstanceDisposed<T>(T configInstance) where T : ISettingBase;
}

#endregion

#region GameEvents

/// <summary>
/// Called as soon as round begins to load before any loading takes place.
/// </summary>
public interface IEventRoundStarting : IEvent<IEventRoundStarting>
{
    void OnRoundStarting();
    static IEventRoundStarting IEvent<IEventRoundStarting>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnRoundStarting = ReturnVoid.Arguments(() => luaFunc[nameof(OnRoundStarting)]()) 
    }.ActLike<IEventRoundStarting>();
}

/// <summary>
/// Called when a round has started and fully loaded.
/// </summary>
public interface IEventRoundStarted : IEvent<IEventRoundStarted>
{
    void OnRoundStart();
    static IEventRoundStarted IEvent<IEventRoundStarted>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnRoundStart = ReturnVoid.Arguments(() => luaFunc[nameof(OnRoundStart)]())
    }.ActLike<IEventRoundStarted>();
}

/// <summary>
/// Called on game loop normal update.
/// </summary>
public interface IEventUpdate : IEvent<IEventUpdate>
{
    void OnUpdate(double fixedDeltaTime);
    static IEventUpdate IEvent<IEventUpdate>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnUpdate = ReturnVoid.Arguments<double>((fixedDeltaTime) => luaFunc[nameof(OnUpdate)](fixedDeltaTime))
    }.ActLike<IEventUpdate>();
}

/// <summary>
/// Called on game loop draw update.
/// </summary>
public interface IEventDrawUpdate : IEvent<IEventDrawUpdate>
{
    void OnDrawUpdate(double deltaTime);
    static IEventDrawUpdate IEvent<IEventDrawUpdate>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnDrawUpdate = ReturnVoid.Arguments<double>((deltaTime) => luaFunc[nameof(OnDrawUpdate)](deltaTime))
    }.ActLike<IEventDrawUpdate>();
}

#endregion

#region Networking


#region Networking-Server
#if SERVER
/// <summary>
/// Called when a client connects to the server and has loaded into the lobby.
/// </summary>
interface IEventClientConnected : IEvent<IEventClientConnected>
{
    /// <summary>
    /// Called when a client connects to the server.
    /// </summary>
    /// <param name="client">The connecting client.</param>
    void OnClientConnected(Client client);
    static IEventClientConnected IEvent<IEventClientConnected>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnClientConnected = ReturnVoid.Arguments<Client>((client) => luaFunc[nameof(OnClientConnected)](client))
    }.ActLike<IEventClientConnected>();
}
#endif
#endregion

#region Networking-Client
#if CLIENT
/// <summary>
/// Called when the client has connected to the server and loaded to the lobby.
/// </summary>
public interface IEventServerConnected : IEvent<IEventServerConnected>
{
    void OnServerConnected();
    static IEventServerConnected IEvent<IEventServerConnected>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) =>  new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnServerConnected = ReturnVoid.Arguments(() => luaFunc[nameof(OnServerConnected)]())
    }.ActLike<IEventServerConnected>();
}
#endif
#endregion

#endregion

#region Assembly_PluginEvents

/// <summary>
/// Called on plugin normal, use this for basic/core loading that does not rely on any other modded content.
/// </summary>
public interface IEventPluginInitialize : IEvent<IEventPluginInitialize>
{
    void Initialize();
    static IEventPluginInitialize IEvent<IEventPluginInitialize>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        Initialize = ReturnVoid.Arguments(() => luaFunc[nameof(Initialize)]())
    }.ActLike<IEventPluginInitialize>();
}

/// <summary>
/// Called once all plugins have been loaded. if you have integrations with any other mod, put that code here.
/// </summary>
public interface IEventPluginLoadCompleted : IEvent<IEventPluginLoadCompleted>
{
    void OnLoadCompleted();
    static IEventPluginLoadCompleted IEvent<IEventPluginLoadCompleted>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnLoadCompleted = ReturnVoid.Arguments(() => luaFunc[nameof(OnLoadCompleted)]())
    }.ActLike<IEventPluginLoadCompleted>();
}

/// <summary>
/// Called before Barotrauma initializes plugins. Use if you want to patch another plugin's behaviour 'unofficially'.
/// WARNING: This method is called before Initialize()!
/// </summary>
public interface IEventPluginPreInitialize : IEvent<IEventPluginPreInitialize>
{
    void PreInitPatching();
    static IEventPluginPreInitialize IEvent<IEventPluginPreInitialize>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnPreInitialize = ReturnVoid.Arguments(() => luaFunc[nameof(PreInitPatching)]())
    }.ActLike<IEventPluginPreInitialize>();
}

/// <summary>
/// Called whenever a new assembly is loaded.
/// </summary>
public interface IEventAssemblyLoaded : IEvent<IEventAssemblyLoaded>
{
    void OnAssemblyLoaded(Assembly assembly);
}

/// <summary>
/// Called whenever an <see cref="IAssemblyLoaderService"/> is instanced.
/// </summary>
public interface IEventAssemblyContextCreated : IEvent<IEventAssemblyContextCreated>
{
    void OnAssemblyCreated(IAssemblyLoaderService loaderService);
}

/// <summary>
/// Called whenever an <see cref="IAssemblyLoaderService"/> begins unloading.
/// </summary>
public interface IEventAssemblyContextUnloading : IEvent<IEventAssemblyContextUnloading>
{
    void OnAssemblyUnloading(WeakReference<IAssemblyLoaderService> loaderService);
}

public interface IEventAssemblyUnloading : IEvent<IEventAssemblyUnloading>
{
    void OnAssemblyUnloading(Assembly assembly);
}

#endregion
