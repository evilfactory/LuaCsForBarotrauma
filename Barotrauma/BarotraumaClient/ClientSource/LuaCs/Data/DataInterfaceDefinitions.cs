using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public partial record ModConfigInfo : IModConfigInfo
{
    public ImmutableArray<IStylesResourceInfo> StylesResourceInfos { get; init; }
}

public record StylesResourceInfo : IStylesResourceInfo
{
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public bool Optional { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public string InternalName { get; init; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
}
