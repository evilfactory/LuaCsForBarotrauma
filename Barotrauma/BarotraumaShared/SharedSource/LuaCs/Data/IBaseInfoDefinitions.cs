using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IPlatformInfo
{
    /// <summary>
    /// Platforms that these localization files should be loaded for.
    /// </summary>
    Platform SupportedPlatforms { get; }
    
    /// <summary>
    /// Targets that these localization files should be loaded for.
    /// </summary>
    Target SupportedTargets { get; }   
}


/// <summary>
/// ResourceInfos contain metadata about a resource.
/// </summary>
public interface IResourceInfo : IPlatformInfo
{
    /// <summary>
    /// Resource absolute file paths.
    /// </summary>
    ImmutableArray<string> FilePaths { get; }
}

/// <summary>
/// Information about supported cultures. It is intended to be ignored if the array is ImmutableArray.Empty .
/// </summary>
public interface IResourceCultureInfo
{
    /// <summary>
    /// List of supported cultures by this resource.
    /// </summary>
    ImmutableArray<CultureInfo> SupportedCultures { get; }
}


public interface ILazyLoadableResourceInfo
{
    /// <summary>
    /// The name that will be used when trying to reference this resource for execution or loading.
    /// </summary>
    public string InternalName { get; }
    
    /// <summary>
    /// Should this be compiled/loaded immediately or stored until demanded.
    /// </summary>
    public bool LazyLoad { get; }
}
