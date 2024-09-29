using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public partial interface IModConfigInfo
{
    /// <summary>
    /// Collection of loadable styles data.
    /// </summary>
    ImmutableArray<IStylesResourceInfo> StylesResourceInfos { get; }
}
