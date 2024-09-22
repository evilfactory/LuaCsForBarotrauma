using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface ILocalizationResourceInfo
{
    CultureInfo TargetCulture { get; }
    ImmutableArray<string> FilePaths { get; }
}
