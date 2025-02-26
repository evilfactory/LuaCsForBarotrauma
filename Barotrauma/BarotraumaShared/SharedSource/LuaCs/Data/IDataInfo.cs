using System;
using System.Collections.Generic;

namespace Barotrauma.LuaCs.Data;

/// <summary>
/// Serves as a compound-key to refer to all resources and information that comes from a specific source.
/// </summary>
public interface IDataInfo : IEqualityComparer<IDataInfo>, IEquatable<IDataInfo>
{
    /// <summary>
    /// Internal name unique within the resources inside a package.
    /// </summary>
    string InternalName { get; }
    /// <summary>
    /// The package this information belongs to.
    /// </summary>
    ContentPackage OwnerPackage { get; }

    bool IEqualityComparer<IDataInfo>.Equals(IDataInfo x, IDataInfo y)
    {
        if (x is null || y is null)
            return false;
        if (x.OwnerPackage is null)
            throw new NullReferenceException($"ContentPackage not set for resource {x}!");
        if (y.OwnerPackage is null)
            throw new NullReferenceException($"ContentPackage not set for resource {y}!");
        if (x.InternalName.IsNullOrWhiteSpace())
            throw new NullReferenceException($"InternalName not set for resource {x}!");
        if (y.InternalName.IsNullOrWhiteSpace())
            throw new NullReferenceException($"InternalName not set for resource {y}!");
        return x.OwnerPackage == y.OwnerPackage && x.InternalName == y.InternalName;
    }

    bool IEquatable<IDataInfo>.Equals(IDataInfo other)
    {
        return Equals(this, other);
    }

    int IEqualityComparer<IDataInfo>.GetHashCode(IDataInfo obj)
    {
        if (obj.OwnerPackage is null)
            throw new NullReferenceException($"ContentPackage not set for resource {obj}!");
        if (obj.InternalName.IsNullOrWhiteSpace())
            throw new NullReferenceException($"InternalName is null for object {obj}!");
        return obj.InternalName.GetHashCode() + obj.OwnerPackage.GetHashCode();
    }
}
