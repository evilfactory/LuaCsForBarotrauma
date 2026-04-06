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
    /// Whether usernames are anonymized or show in logs. 
    /// </summary>
    public bool HideUserNamesInLogs { get; }
    
    /// <summary>
    /// Whether file system caching is enabled.
    /// </summary>
    public bool UseCaching { get; }
    
    /// <summary>
    /// The current state of the Execution State Machine.
    /// </summary>
    public RunState CurrentRunState { get; }
    
    /// <summary>
    /// Returns the best-matching LuaCsForBarotrauma package (enabled list > localMods > WorkshopMods).
    /// </summary>
    public ContentPackage LuaCsForBarotraumaPackage { get; }
}
