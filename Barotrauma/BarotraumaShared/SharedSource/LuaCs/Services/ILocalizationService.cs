using System;
using System.Globalization;
using System.Collections.Generic;

namespace Barotrauma.LuaCs.Services;

public interface ILocalizationService : IService
{
    IReadOnlyCollection<CultureInfo> GetLoadedLocales();
    bool TryLoadXmlFiles(in string[] filePaths, CultureInfo defaultCulture);
    void UnloadAll();
    bool TrySetCurrentCulture(CultureInfo culture);
    bool TrySetCurrentCulture(string cultureName);
    string GetLocalizedString(string key, string fallback);
    string GetLocalizedString(string key, CultureInfo targetCulture);
    bool TryRegisterLocalizationResolver(CultureInfo targetCulture, Func<string, CultureInfo, string> factoryResolver);
    bool ReplaceSymbols(string text, string symbolExpr);
}
