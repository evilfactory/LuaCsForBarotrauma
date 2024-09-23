using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IResourceCultureInfo
{
    ImmutableArray<CultureInfo> SupportedCultures { get; init; }
}
