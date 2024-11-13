namespace Barotrauma.LuaCs.Data;

/// <summary>
/// Serves as a compound-key to refer to all resources and information that comes from a specific source.
/// </summary>
public interface IDataInfo
{
    /// <summary>
    /// Package-Unique name to be used internally for all representations of, and references to, this information.
    /// </summary>
    string InternalName { get; }
    /// <summary>
    /// The package this information belongs to.
    /// </summary>
    ContentPackage OwnerPackage { get; }
    /// <summary>
    /// Used in place of the package data when the OwnerPackage is missing.
    /// </summary>
    string FallbackPackageName { get; }
}
