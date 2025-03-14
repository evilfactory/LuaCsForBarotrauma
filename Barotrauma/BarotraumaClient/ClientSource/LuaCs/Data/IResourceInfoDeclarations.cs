using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public partial interface IModConfigInfo : IStylesResourcesInfo { }

public interface IStylesResourceInfo : IResourceInfo, IResourceCultureInfo, IDataInfo, IPackageDependenciesInfo { }

public interface IStylesResourcesInfo
{
    /// <summary>
    /// Collection of loadable styles data.
    /// </summary>
    ImmutableArray<IStylesResourceInfo> Styles { get; }
}
