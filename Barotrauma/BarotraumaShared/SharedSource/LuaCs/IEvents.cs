using System;
using System.Collections.Generic;
using System.Reflection;
using Barotrauma.Items.Components;
using Barotrauma.LuaCs.Data;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Events;

/*
 * The following is a collection of interfaces that types can implement to be registered events.
 * Note: Internally-marked interfaces should be consumed using a publicizer. This is due to the Barotrauma source
 * types being internal by default.
*/

public interface IEvent
{
    bool IsLuaRunner() => false;
    
    public abstract class LuaWrapperBase : IEvent
    {
        protected readonly IDictionary<string, LuaCsFunc> LuaFuncs;
        protected LuaWrapperBase(IDictionary<string, LuaCsFunc> luaFuncs) => LuaFuncs = luaFuncs;
        public bool IsLuaRunner() => true;
    }
}

public interface IEvent<out T> : IEvent where T : class, IEvent<T>
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

internal interface IEventAfflictionUpdate : IEvent<IEventAfflictionUpdate>
{
    void OnAfflictionUpdate(Affliction affliction, CharacterHealth characterHealth, Limb targetLimb, float deltaTime);

    static IEventAfflictionUpdate IEvent<IEventAfflictionUpdate>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) =>
        new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventAfflictionUpdate
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }
        
        public void OnAfflictionUpdate(Affliction affliction, CharacterHealth characterHealth, Limb targetLimb, float deltaTime)
        {
            LuaFuncs[nameof(OnAfflictionUpdate)](affliction, characterHealth, targetLimb, deltaTime);
        }
    }
}

internal interface IEventGiveCharacterJobItems : IEvent<IEventGiveCharacterJobItems>
{
    void OnGiveCharacterJobItems(Character character, WayPoint spawnPoint, bool isPvPMode);

    static IEventGiveCharacterJobItems IEvent<IEventGiveCharacterJobItems>.GetLuaRunner(
        IDictionary<string, LuaCsFunc> luaFunc) => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventGiveCharacterJobItems
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnGiveCharacterJobItems(Character character, WayPoint spawnPoint, bool isPvPMode)
        {
            LuaFuncs[nameof(OnGiveCharacterJobItems)](character, spawnPoint, isPvPMode);
        }
    }
}

internal interface IEventCharacterCreated : IEvent<IEventCharacterCreated>
{
    void OnCharacterCreated(Character character);

    static IEventCharacterCreated IEvent<IEventCharacterCreated>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventCharacterCreated
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }
        
        public void OnCharacterCreated(Character character)
        {
            LuaFuncs[nameof(OnCharacterCreated)](character);
        }
    }
}

internal interface IEventCharacterDeath : IEvent<IEventCharacterDeath>
{
    void OnCharacterDeath(Character character, Affliction causeOfDeathAffliction, CauseOfDeathType causeOfDeathType);

    static IEventCharacterDeath IEvent<IEventCharacterDeath>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventCharacterDeath
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnCharacterDeath(Character character, Affliction causeOfDeathAffliction, CauseOfDeathType causeOfDeathType)
        {
            LuaFuncs[nameof(OnCharacterDeath)](character, causeOfDeathAffliction, causeOfDeathType);
        }
    }
}

/*
internal interface IEventHumanCPRFailed : IEvent<IEventHumanCPRFailed>
{
    void OnHumanCPRFailed(Character character);
    static IEventHumanCPRFailed IEvent<IEventHumanCPRFailed>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnHumanCPRFailed = ReturnVoid.Arguments((Character character) => luaFunc[nameof(OnHumanCPRFailed)](character))
    }.ActLike<IEventHumanCPRFailed>();
}


internal interface IEventHumanCPRSuccess : IEvent<IEventHumanCPRSuccess>
{
    void OnHumanCPRSuccess(Character character);
    static IEventHumanCPRSuccess IEvent<IEventHumanCPRSuccess>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc) => new
    {
        IsLuaRunner = Return<bool>.Arguments(() => true),
        OnHumanCPRSuccess = ReturnVoid.Arguments((Character character) => luaFunc[nameof(OnHumanCPRSuccess)](character))
    }.ActLike<IEventHumanCPRSuccess>();
}
*/

public interface IEventKeyUpdate : IEvent<IEventKeyUpdate>
{
    void OnKeyUpdate(double deltaTime);

    static IEventKeyUpdate IEvent<IEventKeyUpdate>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventKeyUpdate
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnKeyUpdate(double deltaTime)
        {
            LuaFuncs[nameof(OnKeyUpdate)](deltaTime);
        }
    }
}

/// <summary>
/// Called as soon as round begins to load before any loading takes place.
/// </summary>
public interface IEventRoundStarting : IEvent<IEventRoundStarting>
{
    void OnRoundStarting();

    static IEventRoundStarting IEvent<IEventRoundStarting>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);
    
    public sealed class LuaWrapper : LuaWrapperBase, IEventRoundStarting
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnRoundStarting()
        {
            LuaFuncs[nameof(OnRoundStarting)]();
        }
    }
}

/// <summary>
/// Called when a round has started and fully loaded.
/// </summary>
public interface IEventRoundStarted : IEvent<IEventRoundStarted>
{
    void OnRoundStart();

    static IEventRoundStarted IEvent<IEventRoundStarted>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);
    
    public sealed class LuaWrapper : LuaWrapperBase, IEventRoundStarted
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnRoundStart()
        {
            LuaFuncs[nameof(OnRoundStart)]();
        }
    }
}

/// <summary>
/// Called when a round has ended.
/// </summary>
public interface IEventRoundEnded : IEvent<IEventRoundEnded>
{
    void OnRoundEnd();

    static IEventRoundEnded IEvent<IEventRoundEnded>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventRoundEnded
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnRoundEnd()
        {
            LuaFuncs[nameof(OnRoundEnd)]();
        }
    }
}

internal interface IEventMissionsEnded : IEvent<IEventMissionsEnded>
{
    void OnMissionsEnded(IReadOnlyList<Mission> missions);

    static IEventMissionsEnded IEvent<IEventMissionsEnded>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventMissionsEnded
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnMissionsEnded(IReadOnlyList<Mission> missions)
        {
            LuaFuncs[nameof(OnMissionsEnded)](missions);
        }
    }
}

/// <summary>
/// Called on game loop normal update.
/// </summary>
public interface IEventUpdate : IEvent<IEventUpdate>
{
    void OnUpdate(double fixedDeltaTime);
    static IEventUpdate IEvent<IEventUpdate>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
    => new LuaWrapper(luaFunc);
    
    public sealed class LuaWrapper : LuaWrapperBase, IEventUpdate
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnUpdate(double deltaTime)
        {
            LuaFuncs[nameof(OnUpdate)](deltaTime);
        }
    }
}

/// <summary>
/// Called on game loop draw update.
/// </summary>
public interface IEventDrawUpdate : IEvent<IEventDrawUpdate>
{
    void OnDrawUpdate(double deltaTime);

    static IEventDrawUpdate IEvent<IEventDrawUpdate>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);
    
    public sealed class LuaWrapper : LuaWrapperBase, IEventDrawUpdate
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnDrawUpdate(double deltaTime)
        {
            LuaFuncs[nameof(OnDrawUpdate)](deltaTime);
        }
    }
}

interface IEventSignalReceived : IEvent<IEventSignalReceived>
{
    void OnSignalReceived(Signal signal, Connection connection);

    static IEventSignalReceived IEvent<IEventSignalReceived>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventSignalReceived
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnSignalReceived(Signal signal, Connection connection)
        {
            LuaFuncs[nameof(OnSignalReceived)](signal, connection);
        }
    }
}

#endregion

#region Networking

#region Networking-Server
#if SERVER
public interface IEventClientRawNetMessageReceived : IEvent<IEventClientRawNetMessageReceived>
{
    void OnReceivedClientNetMessage(IReadMessage netMessage, ClientPacketHeader serverPacketHeader, NetworkConnection sender);
}

/// <summary>
/// Called when a client connects to the server.
/// </summary>
interface IEventClientConnected : IEvent<IEventClientConnected>
{
    /// <summary>
    /// Called when a client connects to the server.
    /// </summary>
    /// <param name="client">The connecting client.</param>
    void OnClientConnected(Client client);

    static IEventClientConnected IEvent<IEventClientConnected>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);
    
    public sealed class LuaWrapper : LuaWrapperBase, IEventClientConnected
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnClientConnected(Client client)
        {
            LuaFuncs[nameof(OnClientConnected)](client);   
        }
    }
}

/// <summary>
/// Called when a client disconnects from the server.
/// </summary>
interface IEventClientDisconnected : IEvent<IEventClientDisconnected>
{
    /// <summary>
    /// Called when a client connects to the server.
    /// </summary>
    /// <param name="client">The connecting client.</param>
    void OnClientDisconnected(Client client);

    static IEventClientDisconnected IEvent<IEventClientDisconnected>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventClientDisconnected
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnClientDisconnected(Client client)
        {
            LuaFuncs[nameof(OnClientDisconnected)](client);
        }
    }
}

interface IEventJobsAssigned : IEvent<IEventJobsAssigned>
{
    /// <summary>
    /// Called when a client connects to the server.
    /// </summary>
    /// <param name="client">The connecting client.</param>
    void OnJobsAssigned(IReadOnlyList<Client> unassignedClients);

    static IEventJobsAssigned IEvent<IEventJobsAssigned>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);

    public sealed class LuaWrapper : LuaWrapperBase, IEventJobsAssigned
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnJobsAssigned(IReadOnlyList<Client> unassignedClients)
        {
            LuaFuncs[nameof(OnJobsAssigned)](unassignedClients);
        }
    }
}
#endif

#endregion

#region Networking-Client
#if CLIENT

public interface IEventServerRawNetMessageReceived : IEvent<IEventServerRawNetMessageReceived>
{
    void OnReceivedServerNetMessage(IReadMessage netMessage, ServerPacketHeader serverPacketHeader);
}

/// <summary>
/// Called when the client has connected to the server and loaded to the lobby.
/// </summary>
public interface IEventServerConnected : IEvent<IEventServerConnected>
{
    void OnServerConnected();

    static IEventServerConnected IEvent<IEventServerConnected>.GetLuaRunner(IDictionary<string, LuaCsFunc> luaFunc)
        => new LuaWrapper(luaFunc);
    
    public sealed class LuaWrapper : LuaWrapperBase, IEventServerConnected
    {
        public LuaWrapper(IDictionary<string, LuaCsFunc> luaFuncs) : base(luaFuncs)
        {
        }

        public void OnServerConnected()
        {
            LuaFuncs[nameof(OnServerConnected)]();
        }
    }
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
}

/// <summary>
/// Called once all plugins have been loaded. if you have integrations with any other mod, put that code here.
/// </summary>
public interface IEventPluginLoadCompleted : IEvent<IEventPluginLoadCompleted>
{
    void OnLoadCompleted();
}

/// <summary>
/// Called before Barotrauma initializes plugins. Use if you want to patch another plugin's behaviour 'unofficially'.
/// WARNING: This method is called before Initialize()!
/// </summary>
public interface IEventPluginPreInitialize : IEvent<IEventPluginPreInitialize>
{
    void PreInitPatching();
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
