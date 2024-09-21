namespace Barotrauma.LuaCs.Data;

public interface IPackageDependencyInfo
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
    public int SteamId { get; }
    /// <summary>
    /// The dependency package, if found.
    /// </summary>
    public ContentPackage DependencyPackage { get; }
}
