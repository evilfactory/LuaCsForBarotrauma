using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public interface IStylesResourceInfo : IResourceInfo, IResourceCultureInfo, ILoadableResourceInfo, IPackageDependenciesInfo { }

public interface IStylesResourcesInfo
{
    /// <summary>
    /// Collection of loadable styles data.
    /// </summary>
    ImmutableArray<IStylesResourceInfo> StylesResourceInfos { get; }
}
