using System;
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
/// All info we should have on a package for a given resource.
/// </summary>
public interface IPackageInfo : IDataInfo { }


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
    
    /// <summary>
    /// Marks this resource as optional (ie. Cross-CP content). Setting this to true will allow the dependency system to
    /// try and order the loading but not fail if it runs into circular dependency issues.
    /// </summary>
    bool Optional { get; }
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
