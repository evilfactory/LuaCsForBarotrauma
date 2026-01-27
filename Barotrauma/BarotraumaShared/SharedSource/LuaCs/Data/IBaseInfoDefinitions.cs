using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IDependencyInfo
{
    /// <summary>
    /// List of dependency packages required by this resource.
    /// </summary>
    ImmutableArray<Identifier> RequiredPackages { get; }
    /// <summary>
    /// List of packages incompatible with this resource.
    /// </summary>
    ImmutableArray<Identifier> IncompatiblePackages { get; }
}

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
/// ResourceInfos contain metadata about a resource.
/// </summary>
public interface IResourceInfo : IPlatformInfo
{
    /// <summary>
    /// [Optional]
    /// Specifies the loading order for all assets of the same type (ie. styles, assemblies, etc.) from
    /// the same <see cref="ContentPackage"/>. Lower number is higher priority, see <see cref="System.Linq.Enumerable.OrderBy{TSource,TKey}(IEnumerable{TSource}, Func{TSource,TKey})"/>
    /// </summary>
    int LoadPriority { get; }
    
    /// <summary>
    /// Resource absolute file paths.
    /// </summary>
    [Required]
    ImmutableArray<ContentPath> FilePaths { get; }
    
    /// <summary>
    /// Marks this resource as optional (ie. Cross-CP content). Setting this to true will allow the dependency system to
    /// try and order the loading but not fail if it runs into circular dependency issues.
    /// </summary>
    bool Optional { get; }
}
