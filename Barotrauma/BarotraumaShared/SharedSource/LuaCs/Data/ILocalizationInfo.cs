using System.Collections.Generic;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface ILocalizationInfo : IDataInfo
{
    int LoadPriority { get; }
    string Key { get; }
    IReadOnlyList<(CultureInfo Culture, string Value)> Translations { get; }
}
