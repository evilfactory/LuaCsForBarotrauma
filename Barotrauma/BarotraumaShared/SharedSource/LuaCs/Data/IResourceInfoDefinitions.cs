using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IConfigResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo { }
public interface IConfigProfileResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo { }
public interface ILocalizationResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo { }
/// <summary>
/// Represents loadable Lua files.
/// </summary>
public interface ILuaResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, ILazyLoadableResourceInfo { }
public interface IAssemblyResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, ILazyLoadableResourceInfo
{
    /// <summary>
    /// The friendly name of the assembly. Script files belonging to the same assembly should all have the same name.
    /// Legacy scripts will all be given the sanitized name of the Content Package they belong to.
    /// </summary>
    public string FriendlyName { get; }
    /// <summary>
    /// Is this entry referring to a script file collection.
    /// </summary>
    public bool IsScript { get; }
}
