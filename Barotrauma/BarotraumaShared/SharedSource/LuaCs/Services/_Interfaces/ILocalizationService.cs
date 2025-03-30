using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface ILocalizationService : IReusableService
{
    /// <summary>
    /// Gets all available locale definitions from the current runtime environment.
    /// </summary>
    /// <returns></returns>
    IReadOnlyCollection<CultureInfo> GetLoadedLocales();
    internal void DisposePackage(ContentPackage package);
    internal FluentResults.Result SetCurrentCulture(CultureInfo culture);
    internal FluentResults.Result SetCurrentCulture(string cultureName);
    internal Task<FluentResults.Result> LoadLocalizations(ImmutableArray<ILocalizationResourceInfo> localizationResources);
    FluentResults.Result<string> GetLocalizedStringForPackage(ContentPackage package, string key);
    FluentResults.Result<string> GetLocalizedStringForPackage(ContentPackage package, string key, CultureInfo targetCulture);
    string GetLocalizedStringForPackage(ContentPackage package, string key, string fallback);
    string GetLocalizedStringForPackage(ContentPackage package, string key, string fallback, CultureInfo targetCulture);
    bool IsCurrentCultureSupported(IResourceCultureInfo culturesInfo);
}

