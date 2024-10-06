using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public partial class ModConfigInfo : IModConfigInfo
{
    public ImmutableArray<IStylesResourceInfo> StylesResourceInfos { get; init; }
}
