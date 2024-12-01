using System.Reflection;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Events;

/*
 * The following is a collection of interfaces that types can implement to be registered events.
 * Note: Internally-marked interfaces should be consumed using a publicizer. This is due to the Barotrauma source
 * types being internal by default.
*/

public interface IEvent { }

#region GameEvents

/// <summary>
/// Called as soon as round begins to load before any loading takes place.
/// </summary>
public interface IEventRoundStarting : IEvent
{
    void OnRoundStarting();
}

/// <summary>
/// Called when a round has started and fully loaded.
/// </summary>
public interface IEventRoundStarted : IEvent
{
    void OnRoundStart();
}

/// <summary>
/// Called on game loop normal update.
/// </summary>

public interface IEventUpdate : IEvent
{
    void OnUpdate();
}

/// <summary>
/// Called on game loop fixed update (physics)
/// </summary>
public interface IEventFixedUpdate : IEvent
{
    void OnFixedUpdate();
}

#endregion

#region Networking


#region Networking-Server
#if SERVER
/// <summary>
/// Called when a client connects to the server and has loaded into the lobby.
/// </summary>
interface IEventClientConnected : IEvent
{
    /// <summary>
    /// Called when a client connects to the server.
    /// </summary>
    /// <param name="client">The connecting client.</param>
    void OnClientConnected(Client client);
}
#endif
#endregion

#region Networking-Client
#if CLIENT
/// <summary>
/// Called when the client has connected to the server and loaded to the lobby.
/// </summary>
public interface IEventServerConnected : IEvent
{
    void OnServerConnected();
}
#endif
#endregion

#endregion

#region Assembly_PluginEvents

/// <summary>
/// Allows registration of events and services before plugins are initialized.
/// </summary>
public interface IEventTypeRegistrationProvider : IEvent
{
    void RegisterEvents(IPluginEventService service);
    void UnregisterEvents(IPluginEventService service);
}

/// <summary>
/// Called on plugin normal, use this for basic/core loading that does not rely on any other modded content.
/// </summary>
public interface IEventPluginInitialize : IEvent
{
    void Initialize();
}

/// <summary>
/// Called once all plugins have been loaded. if you have integrations with any other mod, put that code here.
/// </summary>
public interface IEventPluginLoadCompleted : IEvent
{
    void OnLoadCompleted();
}

/// <summary>
/// Called before Barotrauma initializes plugins. Use if you want to patch another plugin's behaviour 'unofficially'.
/// WARNING: This method is called before Initialize()!
/// </summary>
public interface IEventPluginPreInitialize : IEvent
{
    void PreInitPatching();
}

/// <summary>
/// Called whenever a new assembly is loaded.
/// </summary>
public interface IEventAssemblyLoaded : IEvent
{
    void OnAssemblyLoaded(Assembly assembly);
}

/// <summary>
/// Called whenever an <see cref="AssemblyLoader"/> begins unloading.
/// </summary>
public interface IEventAssemblyContextUnloaded : IEvent
{
    void OnAssemblyUnloaded(AssemblyLoader ctx);
}

#endregion
