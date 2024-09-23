using System.Collections.Generic;
using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

/// <summary>
/// ResourceInfos contain metadata about a resource.
/// </summary>
public interface IResourceInfo
{
    /// <summary>
    /// Platforms that these localization files should be loaded for.
    /// </summary>
    Platform SupportedPlatforms { get; }
    
    /// <summary>
    /// Targets that these localization files should be loaded for.
    /// </summary>
    Target SupportedTargets { get; }
    
    /// <summary>
    /// Resource absolute file paths.
    /// </summary>
    ImmutableArray<string> FilePaths { get; }
}
