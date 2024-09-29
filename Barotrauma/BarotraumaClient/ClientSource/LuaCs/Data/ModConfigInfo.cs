using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public readonly partial struct ModConfigInfo : IModConfigInfo
{
    public ImmutableArray<IStylesResourceInfo> StylesResourceInfos { get; init; }
}
