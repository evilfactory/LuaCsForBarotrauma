using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IPlatformInfo
{
    /// <summary>
    /// Platforms that these localization files should be loaded for.
    /// </summary>
    [Required]
    Platform SupportedPlatforms { get; }
    
    /// <summary>
    /// Targets that these localization files should be loaded for.
    /// </summary>
    [Required]
    Target SupportedTargets { get; }   
}


/// <summary>
/// Which package does the following data belong to?
/// </summary>
public interface IPackageInfo
{
    ContentPackage OwnerPackage { get; }
}


/// <summary>
/// ResourceInfos contain metadata about a resource.
/// </summary>
public interface IResourceInfo : IPlatformInfo
{
    /// <summary>
    /// [Optional]
    /// Allows you to specify the loading order for all assets of the same type (ie. styles, assemblies, etc.).
    /// </summary>
    int LoadPriority { get; }
    
    /// <summary>
    /// Resource absolute file paths.
    /// </summary>
    [Required]
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
    [Required]
    public string InternalName { get; }
    
    /// <summary>
    /// Should this be compiled/loaded immediately or stored until demanded.
    /// </summary>
    public bool LazyLoad { get; }
}
