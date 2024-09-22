namespace Barotrauma.LuaCs.Data;

public interface IResourceInfo
{
    /// <summary>
    /// Platforms that these localization files should be loaded for.
    /// </summary>
    Platform SupportedPlatforms { get; }
    
    /// <summary>
    /// Targets that these localization files should be loaded for.
    /// </summary>
    Target SupportedTargets { get; init; }
}
