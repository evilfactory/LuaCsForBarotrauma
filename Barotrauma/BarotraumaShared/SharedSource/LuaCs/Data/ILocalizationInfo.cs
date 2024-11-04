using System.Collections.Generic;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface ILocalizationInfo : IDataInfo
{
    string Symbol { get; }
    IReadOnlyDictionary<CultureInfo, RawLString> LocalizedValues { get; }
    RawLString GetLocalizedString(CultureInfo locale);
    RawLString GetLocalizedString(string cultureCode);
}
