using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface ILocalizationService : IReusableService
{
    IReadOnlyCollection<CultureInfo> GetLoadedLocales();
    void Remove(ImmutableArray<ILocalizationResourceInfo> localizations);
    void DisposePackage(ContentPackage package);
    FluentResults.Result SetCurrentCulture(CultureInfo culture);
    FluentResults.Result SetCurrentCulture(string cultureName);
    Task<FluentResults.Result> LoadLocalizations(ImmutableArray<ILocalizationResourceInfo> localizationResources);
    
    /// <summary>
    /// Tries to get a localized string without a fallback. Returns success/failure and associated data.
    /// </summary>
    /// <param name="key">Neutral localization key.</param>
    /// <returns></returns>
    FluentResults.Result<string> GetLocalizedString(string key);
    FluentResults.Result<string> GetLocalizedString(string key, CultureInfo targetCulture);
    string GetLocalizedString(string key, string fallback);
    string GetLocalizedString(string key, string fallback, CultureInfo targetCulture);
    FluentResults.Result<string> GetLocalizedStringForPackage(ContentPackage package, string key);
    FluentResults.Result<string> GetLocalizedStringForPackage(ContentPackage package, string key, CultureInfo targetCulture);
    string GetLocalizedStringForPackage(ContentPackage package, string key, string fallback);
    string GetLocalizedStringForPackage(ContentPackage package, string key, string fallback, CultureInfo targetCulture);
    FluentResults.Result RegisterLocalizationResolver(CultureInfo targetCulture, Func<string, CultureInfo, string> factoryResolver);
    bool IsCurrentCultureSupported(IResourceCultureInfo culturesInfo);
}
