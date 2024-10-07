using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public interface IPackageDependencyInfo : IPackageInfo
{
    /// <summary>
    /// Root folder of the content package.
    /// </summary>
    public string FolderPath { get; }
    /// <summary>
    /// Name of the package.
    /// </summary>
    public string PackageName { get; }
    /// <summary>
    /// Steam ID of the package. 
    /// </summary>
    public ulong SteamWorkshopId { get; }
    /// <summary>
    /// The dependency package, if found in the ALL Packages List.
    /// </summary>
    public ContentPackage DependencyPackage { get; }
    /// <summary>
    /// Marks this dependency optional (ie. Cross-CP content). Setting this to true will allow the dependency system to
    /// try and order the loading but not fail if it runs into circular dependency issues.
    /// </summary>
    bool Optional { get; }
}

public interface IPackageDependenciesInfo
{
    /// <summary>
    /// List of required packages.
    /// </summary>
    ImmutableArray<IPackageDependencyInfo> Dependencies { get; }
}
