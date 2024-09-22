using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface ILocalizationResourceInfo : IResourceInfo
{
    /// <summary>
    /// The target culture (languages) that this localization data is intended for.
    /// </summary>
    CultureInfo TargetCulture { get; }
    /// <summary>
    /// List of localization files with their absolute path.
    /// </summary>
    ImmutableArray<string> FilePaths { get; }
}
