using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface ILocalizationService : IService
{
    IReadOnlyCollection<CultureInfo> GetLoadedLocales();
    void Remove(ImmutableArray<ILocalizationResourceInfo> localizations);
    bool TrySetCurrentCulture(CultureInfo culture);
    bool TrySetCurrentCulture(string cultureName);
    bool TryLoadLocalizations(ImmutableArray<ILocalizationResourceInfo> localizationResources);
    string GetLocalizedString(string key, string fallback);
    string GetLocalizedString(string key, CultureInfo targetCulture);
    bool TryRegisterLocalizationResolver(CultureInfo targetCulture, Func<string, CultureInfo, string> factoryResolver);
    bool ReplaceSymbols(string text, string symbolExpr);
    bool IsCurrentCultureSupported(IResourceCultureInfo culturesInfo);
}
