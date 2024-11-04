using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Events;

/*
 * The following is a collection of interfaces that types can implement to be registered events.
 * Note: Internally-marked interfaces should be consumed using a publicizer. This is due to the Barotrauma source
 * types being internal by default.
*/

#region GameEvents

/// <summary>
/// Called as soon as round begins to load before any loading takes place.
/// </summary>
public interface IEventRoundStarting
{
    void OnRoundStarting();
}

/// <summary>
/// Called when a round has started and fully loaded.
/// </summary>
public interface IEventRoundStarted
{
    void OnRoundStart();
}

/// <summary>
/// Called on game loop normal update.
/// </summary>

public interface IEventUpdate
{
    void OnUpdate();
}

/// <summary>
/// Called on game loop fixed update (physics)
/// </summary>
public interface IEventFixedUpdate
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
interface IEventClientConnected
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
public interface IEventServerConnected 
{
    void OnServerConnected();
}
#endif
#endregion

#endregion

#region PluginEvents

/// <summary>
/// Called on plugin normal, use this for basic/core loading that does not rely on any other modded content.
/// </summary>
public interface IEventPluginInitialize
{
    void Initialize();
}

/// <summary>
/// Called once all plugins have been loaded. if you have integrations with any other mod, put that code here.
/// </summary>
public interface IEventPluginLoadCompleted
{
    void OnLoadCompleted();
}

/// <summary>
/// Called before Barotrauma initializes plugins. Use if you want to patch another plugin's behaviour 'unofficially'.
/// WARNING: This method is called before Initialize()!
/// </summary>
public interface IEventPluginPreInitialize
{
    void PreInitPatching();
}

#endregion
