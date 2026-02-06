namespace Barotrauma.LuaCs;

/// <summary>
/// Provides access to data from the current <see cref="LuaCsSetup"/>.
/// </summary>
public interface ILuaCsInfoProvider : IService
{
    /// <summary>
    /// Whether C# plugin code is enabled.
    /// </summary>
    public bool IsCsEnabled { get; }

    /// <summary>
    /// Whether the popup error GUI should be hidden/suppressed.
    /// </summary>
    public bool DisableErrorGUIOverlay { get; }

    /// <summary>
    /// Whether usernames are anonymized or show in logs. 
    /// </summary>
    public bool HideUserNamesInLogs { get; }

    /// <summary>
    /// The SteamId of the Workshop LuaCs CPackage in use, if available.
    /// </summary>
    public ulong LuaForBarotraumaSteamId { get; }

    /// <summary>
    /// Restrict the maximum size of messages sent over the network.
    /// </summary>
    public bool RestrictMessageSize { get; }

    /// <summary>
    /// The local save path for all local data storage for mods.
    /// </summary>
    public string LocalDataSavePath { get; }
    
    /// <summary>
    /// The current state of the Execution State Machine.
    /// </summary>
    public RunState CurrentRunState { get; }
}
