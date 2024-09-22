using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IConfigResourceInfo : IResourceInfo
{
    string FilePath { get; init; }
    Platform Platforms { get; init; }
    Target Targets { get; init; }
    ImmutableArray<CultureInfo> SupportedCultures { get; init; }
}
