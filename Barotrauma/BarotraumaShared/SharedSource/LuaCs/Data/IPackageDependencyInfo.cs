using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public interface IPackageDependencyInfo : IPackageInfo, 
    IEqualityComparer<IPackageDependencyInfo>
{
    /// <summary>
    /// Root folder of the content package.
    /// </summary>
    public string FolderPath { get; }
    /// <summary>
    /// Steam ID of the package. 
    /// </summary>
    public ulong SteamWorkshopId { get; }
    /// <summary>
    /// The dependency package, if found in the ALL Packages List.
    /// </summary>
    public ContentPackage DependencyPackage { get; }
    
    /// <summary>
    /// This dependency was not found.
    /// </summary>
    public bool IsMissing { get; }
    
    /// <summary>
    /// Whether the package is installed from the workshop. False means installation is from local mods.
    /// </summary>
    public bool IsWorkshopInstallation { get; }
}

public interface IPackageDependenciesInfo
{
    /// <summary>
    /// List of required packages.
    /// </summary>
    ImmutableArray<IPackageDependencyInfo> Dependencies { get; }
}
