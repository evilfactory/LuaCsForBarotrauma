using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IConfigResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo { }
public interface IConfigProfileResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo { }
public interface ILocalizationResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo { }

public interface IAssemblyResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo
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
    /// <summary>
    /// Should this be compiled/loaded immediately or stored until demanded.
    /// </summary>
    public bool LazyLoad { get; }
}
