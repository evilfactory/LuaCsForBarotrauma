using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

/// <summary>
/// <b>[Supported Features]</b><br/>
/// - Loading Priority.<br/>
/// </summary>
public class LocalizationService : ILocalizationService
{
    private record PackageLocalizations
    {
        public ContentPackage Package { get; init; }
        private readonly ConcurrentDictionary<(CultureInfo Cutlure, string Key), string> _translations;
        public IReadOnlyDictionary<(CultureInfo Cutlure, string Key), string> Translations => _translations;

        public PackageLocalizations(ContentPackage package,
            ImmutableArray<(CultureInfo Culture, string Key, string Value)> translationsList)
        {
            Package = package;
            var dict = new ConcurrentDictionary<(CultureInfo, string), string>();
            
            if (!translationsList.IsDefaultOrEmpty)
            {
                foreach (var translation in translationsList)
                {
                    if (translation.Culture is null || translation.Key is null || translation.Value is null)
                        continue;
                    dict[(translation.Culture, translation.Key)] = translation.Value;
                }
            }

            _translations = dict;
        }

        public void UpsertTranslation(CultureInfo culture, string key, string value)
        {
            if (culture is null || key.IsNullOrWhiteSpace() || value.IsNullOrWhiteSpace())
                return;
            _translations[(culture, key)] = value;
        }
    }

    public CultureInfo CurrentCulture { get; private set; }
    private readonly ConcurrentDictionary<ContentPackage, PackageLocalizations> _packageLocalizations = new();
    private int _isDisposed;
    private readonly IConverterServiceAsync<ILocalizationResourceInfo, ImmutableArray<ILocalizationInfo>> _localizationLoader;
    private readonly Lazy<IPackageManagementService> _packageManagementService;
    
    public LocalizationService(IConverterServiceAsync<ILocalizationResourceInfo, ImmutableArray<ILocalizationInfo>> localizationLoader, 
        Lazy<IPackageManagementService> packageManagementService)
    {
        _localizationLoader = localizationLoader;
        _packageManagementService = packageManagementService;
    }

    public void Dispose()
    {
        IsDisposed = true;
        _packageLocalizations.Clear();
    }

    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    public FluentResults.Result Reset()
    {
        ((IService)this).CheckDisposed();
        _packageLocalizations.Clear();
        return FluentResults.Result.Ok();
    }

    public IReadOnlyCollection<CultureInfo> GetLoadedLocales()
    {
        ((IService)this).CheckDisposed();
        return CultureInfo.GetCultures(CultureTypes.AllCultures).ToImmutableArray();
    }

    public void DisposePackage(ContentPackage package)
    {
        ((IService)this).CheckDisposed();
        _packageLocalizations.TryRemove(package, out _);
    }

    public FluentResults.Result SetCurrentCulture(CultureInfo culture)
    {
        ((IService)this).CheckDisposed();
        if (!CultureInfo.GetCultures(CultureTypes.AllCultures).Contains(culture))
            return FluentResults.Result.Fail($"Culture {culture.Name} is not supported.");
        CurrentCulture = culture;
        return FluentResults.Result.Ok();
    }

    public FluentResults.Result SetCurrentCulture(string cultureName)
    {
        ((IService)this).CheckDisposed();
        if (cultureName.IsNullOrWhiteSpace())
            return FluentResults.Result.Fail($"Culture name was empty.");
        
        try
        {
            CurrentCulture = CultureInfo.GetCultureInfo(cultureName);    
            return FluentResults.Result.Ok();
        }
        catch (CultureNotFoundException e)
        {
            return FluentResults.Result.Fail($"Culture {cultureName} was not found.");
        }
    }

    public async Task<FluentResults.Result> LoadLocalizations(ImmutableArray<ILocalizationResourceInfo> localizationResources)
    {
        ((IService)this).CheckDisposed();
        if (localizationResources.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"Localizations: array is empty.");

        var resArr = await _localizationLoader.TryParseResourcesAsync(
            _packageManagementService.Value.FilterUnloadableResources(localizationResources, false));

        if (resArr.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(LoadLocalizations)}: filtered array is empty.");
        
        Queue<ISuccess> successes = new();
        Queue<IError> errors = new();

        // errors
        var resArrSuccesses = resArr
            .Where(r => r.IsSuccess)
            .SelectMany(r => r.Value)
            .GroupBy(r => r.OwnerPackage);
        // errors to log
        var resArrErrors = resArr
            .Where(r => r.IsFailed)
            .SelectMany(r => r.Errors);

        foreach (var error in resArrErrors)
            errors.Enqueue(error);

        foreach (var packageInfos in resArrSuccesses)
        {
            if (_packageLocalizations.TryGetValue(packageInfos.Key, out var packageLocalizations))
            {
                foreach (var info in packageInfos.OrderByDescending(r => r.LoadPriority))
                {
                    if (info.Translations.Count < 1)
                        continue;
                    foreach (var translation in info.Translations)
                    {
                        packageLocalizations.UpsertTranslation(translation.Culture, info.Key, translation.Value);
                    }
                }
            }
            else
            {
                var translations = packageInfos
                    .Where(p => p.Translations.Count > 0)
                    .SelectMany(p => p.Translations
                        .Select(t => (t.Culture, p.Key, t.Value)))
                    .ToImmutableArray();
                var packLocalizations = new PackageLocalizations(packageInfos.Key, translations);
                _packageLocalizations[packageInfos.Key] = packLocalizations;
            }
            
            successes.Enqueue(new Success($"Loaded localizations for {packageInfos.Key.Name}"));
        }

        bool success = successes.Count > 0;
        FluentResults.Result res;
        if (success)
        {
            res = FluentResults.Result.Ok();
        }
        else
        {
            res = FluentResults.Result.Fail($"Failed to load localizations.");
        }

        while (errors.TryDequeue(out var error))
        {
            res = res.WithError(error);
        }
        
        while (successes.TryDequeue(out var succ))
        {
            res = res.WithSuccess(succ);
        }

        return res;
    }

    public Result<string> GetLocalizedStringForPackage(ContentPackage package, string key) 
        => GetLocalizedStringForPackage(package, key, CurrentCulture);

    public Result<string> GetLocalizedStringForPackage(ContentPackage package, string key, CultureInfo targetCulture)
    {
        ((IService)this).CheckDisposed();
        if (package is null || key.IsNullOrWhiteSpace() || targetCulture is null)
            return FluentResults.Result.Fail($"Package, Culture or key was null or empty.");
        
        if (!_packageLocalizations.TryGetValue(package, out var locs))
            return FluentResults.Result.Fail($"Localizations for {package.Name} was not found.");
        
        if (!locs.Translations.TryGetValue((targetCulture, key), out var value))
            return FluentResults.Result.Fail($"Localizations for key {key} was not found.");
        
        return FluentResults.Result.Ok(value);
    }

    public string GetLocalizedStringForPackage(ContentPackage package, string key, string fallback) 
        => GetLocalizedStringForPackage(package, key, fallback, CurrentCulture);

    public string GetLocalizedStringForPackage(ContentPackage package, string key, string fallback, CultureInfo targetCulture)
    {
        ((IService)this).CheckDisposed();
        if (package is null || key.IsNullOrWhiteSpace() || targetCulture is null)
            return fallback;
        
        if (!_packageLocalizations.TryGetValue(package, out var locs))
            return fallback;
        
        if (!locs.Translations.TryGetValue((targetCulture, key), out var value))
            return fallback;

        return value;
    }

    public bool IsCurrentCultureSupported(IResourceCultureInfo culturesInfo)
    {
        ((IService)this).CheckDisposed();
        if (culturesInfo is null)
            return false;
        return !culturesInfo.SupportedCultures.IsDefaultOrEmpty && culturesInfo.SupportedCultures.Contains(CurrentCulture);
    }
}
