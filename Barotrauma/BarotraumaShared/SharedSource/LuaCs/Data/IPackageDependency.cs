using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Data;

public interface IPackageDependency : IDataInfo, IEquatable<IPackageDependency>
{
    public IPackageInfo Dependency { get; }

    bool IEquatable<IPackageDependency>.Equals(IPackageDependency other)
    {
        return other is not null && Dependency.Equals(other.Dependency);
    }
}

public interface IPackageInfo : IEquatable<IPackageInfo>
{
    /// <summary>
    /// Name of the content package.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Steam ID of the package. 
    /// </summary>
    public ulong SteamWorkshopId { get; }
    /// <summary>
    /// The Guid for the runtime instance of the package.
    /// </summary>
    public uint Id { get; }

    /// <summary>
    /// Gets the reference to the best-match target ContentPackage that meets the requirement.
    /// </summary>
    /// <returns>The <see cref="ContentPackage"/> reference, or null if none was found.</returns>
    public ContentPackage GetPackage();

    /// <summary>
    /// Tries to retrieve the current best <see cref="ContentPackage"/> and returns true if none was found.
    /// </summary>
    public bool IsMissing => GetPackage() is null;

    bool IEquatable<IPackageInfo>.Equals(IPackageInfo other)
    {
        if (other is null) 
            return false;
        if (ReferenceEquals(other, this))
            return true;
        if (!this.IsMissing && !other.IsMissing && ReferenceEquals(other.GetPackage, this.GetPackage))
            return true;
        if (this.SteamWorkshopId != 0 && other.SteamWorkshopId == this.SteamWorkshopId)
            return true;
        return this.Name == other.Name;
    }
}

public interface IPackageDependenciesInfo
{
    /// <summary>
    /// List of required packages.
    /// </summary>
    ImmutableArray<IPackageDependency> Dependencies { get; }
}
