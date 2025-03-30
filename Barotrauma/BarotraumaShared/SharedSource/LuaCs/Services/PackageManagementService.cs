
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;
using Barotrauma.Steam;
using FluentResults;
using OneOf;

// ReSharper disable UseCollectionExpression

namespace Barotrauma.LuaCs.Services;

public partial class PackageManagementService : IPackageManagementService
{
    private int _isDisposed;
    private readonly ConcurrentDictionary<ContentPackage, IModConfigInfo> _modInfos = new();
    // lookup caches
    private readonly IPackageInfoLookupService _packageInfoLookupService;
    // processors
    private readonly IConverterServiceAsync<ContentPackage, IModConfigInfo> _modConfigParserService;
    private readonly IProcessorService<IReadOnlyList<IAssemblyResourceInfo>, IAssembliesResourcesInfo> _assemblyInfoConverter;
    private readonly IProcessorService<IReadOnlyList<IConfigResourceInfo>, IConfigsResourcesInfo> _configsInfoConverter;
    private readonly IProcessorService<IReadOnlyList<IConfigProfileResourceInfo>, IConfigProfilesResourcesInfo> _configProfilesConverter;
    private readonly IProcessorService<IReadOnlyList<ILocalizationResourceInfo>, ILocalizationsResourcesInfo> _localizationsConverter;
    private readonly IProcessorService<IReadOnlyList<ILuaScriptResourceInfo>, ILuaScriptsResourcesInfo> _luaScriptsConverter;


    public void Dispose()
    {
        IsDisposed = true;
        _modInfos.Clear();
    }

    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    public FluentResults.Result Reset()
    {
        try
        {
            ((IService)this).CheckDisposed();
            _modInfos.Clear();
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(new ExceptionalError(e));
        }
        return FluentResults.Result.Ok();
    }

    public ImmutableArray<ILocalizationResourceInfo> Localizations => _modInfos.IsEmpty ? ImmutableArray<ILocalizationResourceInfo>.Empty 
        : _modInfos.SelectMany(kvp => kvp.Value.Localizations).ToImmutableArray();
    public ImmutableArray<IConfigResourceInfo> Configs => _modInfos.IsEmpty ? ImmutableArray<IConfigResourceInfo>.Empty 
        : _modInfos.SelectMany(kvp => kvp.Value.Configs).ToImmutableArray();
    public ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles => _modInfos.IsEmpty ? ImmutableArray<IConfigProfileResourceInfo>.Empty 
        : _modInfos.SelectMany(kvp => kvp.Value.ConfigProfiles).ToImmutableArray();
    public ImmutableArray<ILuaScriptResourceInfo> LuaScripts => _modInfos.IsEmpty ? ImmutableArray<ILuaScriptResourceInfo>.Empty 
        : _modInfos.SelectMany(kvp => kvp.Value.LuaScripts).ToImmutableArray();
    public ImmutableArray<IAssemblyResourceInfo> Assemblies => _modInfos.IsEmpty ? ImmutableArray<IAssemblyResourceInfo>.Empty 
        : _modInfos.SelectMany(kvp => kvp.Value.Assemblies).ToImmutableArray();
    

    public async Task<FluentResults.Result> LoadPackageInfosAsync(ContentPackage package)
    {
        ((IService)this).CheckDisposed();
        if (package is null)
            return FluentResults.Result.Fail(new ExceptionalError(new NullReferenceException($"{nameof(LoadPackageInfosAsync)}: ContentPackage is null.")));
        var result = await _modConfigParserService.TryParseResourceAsync(package);
        if (result.IsFailed)
            return FluentResults.Result.Fail($"$Could not parse package mod config.").WithErrors(result.Errors);
        if (!_modInfos.TryAdd(package, result.Value))
            return FluentResults.Result.Fail($"Failed to add ModInfo for {package.Name}.");
        return FluentResults.Result.Ok();
    }

    public async Task<IReadOnlyList<(ContentPackage, FluentResults.Result)>> LoadPackagesInfosAsync(IReadOnlyList<ContentPackage> packages)
    {
        ((IService)this).CheckDisposed();
        if (packages is null || packages.Count == 0)
            throw new ArgumentNullException(nameof(LoadPackagesInfosAsync));
        ConcurrentQueue<(ContentPackage, FluentResults.Result)> results = new();
        await packages.ParallelForEachAsync(async package =>
        {
            var res = await LoadPackageInfosAsync(package);
            results.Enqueue((package, res));
        }, Environment.ProcessorCount);
        return results.ToImmutableArray();
    }

    public IReadOnlyList<ContentPackage> GetAllLoadedPackages()
    {
        ((IService)this).CheckDisposed();
        return _modInfos.IsEmpty ? ImmutableArray<ContentPackage>.Empty 
            : _modInfos.Select(kvp => kvp.Key).ToImmutableArray();
    }

    public bool IsPackageLoaded(ContentPackage package)
    {
        return package is not null && _modInfos.ContainsKey(package);
    }

    public ImmutableArray<T> FilterUnloadableResources<T>(IReadOnlyList<T> resources, bool enabledPackagesOnly = false) 
        where T : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo
    {
        return resources
            .Where(r => r is not null)
            .Where(r => (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0)
            .Where(r => (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0)
            .Where(r => !r.Dependencies.Any() || r.Dependencies.All(d => 
                            d.Dependency.GetPackage() is {} p   // cp is valid
                            && _modInfos.ContainsKey(p)                      // cp is parsed
                            && (!enabledPackagesOnly || _packageInfoLookupService.IsPackageEnabled(p)))) // cp is enabled
            .ToImmutableArray();
    }

    public void DisposePackageInfos(ContentPackage package)
    {
        _modInfos.TryRemove(package, out _);
    }

    public void DisposePackagesInfos(IReadOnlyList<ContentPackage> packages)
    {
        if (packages is null || packages.Count == 0)
            return;
        
        foreach (var package in packages)
        {
            DisposePackageInfos(package);
        }
    }

    public Result<IPackageDependency> GetPackageDependencyInfo(ContentPackage ownerPackage, string packageName,
        ulong steamWorkshopId)
    {
        ((IService)this).CheckDisposed();
        
        if (ownerPackage is null)
            return FluentResults.Result.Fail($"OwnerPackage is null.");
        var nameGood = !packageName.IsNullOrWhiteSpace();
        
        if (!nameGood && steamWorkshopId == 0)
            FluentResults.Result.Fail($"PackageName and SteamId cannot both be invalid.");

        IPackageInfo depInfo = null;
        
        // complex key
        if (nameGood && steamWorkshopId != 0
                     && _packageInfoLookupService.Lookup(packageName, steamWorkshopId).GetAwaiter().GetResult() is
                     { IsSuccess: true, Value: {} dep1 })
        {
            depInfo = dep1;
        }
        // name key
        else if (nameGood && _packageInfoLookupService.Lookup(packageName).GetAwaiter().GetResult() is
                 { IsSuccess: true, Value: { } dep2 })
        {
            depInfo = dep2;
        }
        // steamid key
        else if (_packageInfoLookupService.Lookup(steamWorkshopId).GetAwaiter().GetResult() is
                 { IsSuccess: true, Value: { } dep3 })
        {
            depInfo = dep3;
        }
        // this should never be null so we return an exception
        else
        {
            return FluentResults.Result.Fail($"Package Dependency for {ownerPackage.Name} was not found.");
        }
        
        return FluentResults.Result.Ok<IPackageDependency>(new PackageDependency(ownerPackage, depInfo, ownerPackage.Name));
    }

    public Result<IAssembliesResourcesInfo> GetAssembliesInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (package is null)
            return FluentResults.Result.Fail($"{nameof(GetAssembliesInfos)}: ContentPackage is null.");
        if (_modInfos.TryGetValue(package, out var result))
            return FluentResults.Result.Ok<IAssembliesResourcesInfo>(_assemblyInfoConverter.Process(onlySupportedResources?
                result.Assemblies.Where(r => 
                    (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                    && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                : result.Assemblies
                ));
        return FluentResults.Result.Fail(
            $"{nameof(GetAssembliesInfos)}: ContentPackage {package.Name} is not registered.");
    }

    public Result<IConfigsResourcesInfo> GetConfigsInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (package is null)
            return FluentResults.Result.Fail($"{nameof(GetConfigsInfos)}: ContentPackage is null.");
        
        if (_modInfos.TryGetValue(package, out var result))
        {
            return FluentResults.Result.Ok<IConfigsResourcesInfo>(_configsInfoConverter.Process(onlySupportedResources?
                result.Configs.Where(r => 
                    (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                    && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                : result.Configs
            ));
        }

        return FluentResults.Result.Fail(
            $"{nameof(GetConfigsInfos)}: ContentPackage {package.Name} is not registered.");
    }

    public Result<IConfigProfilesResourcesInfo> GetConfigProfilesInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (package is null)
            return FluentResults.Result.Fail($"{nameof(GetConfigProfilesInfos)}: ContentPackage is null.");
        
        if (_modInfos.TryGetValue(package, out var result))
        {
            return FluentResults.Result.Ok<IConfigProfilesResourcesInfo>(_configProfilesConverter.Process(onlySupportedResources?
                result.ConfigProfiles.Where(r => 
                    (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                    && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                : result.ConfigProfiles
            ));
        }

        return FluentResults.Result.Fail(
            $"{nameof(GetConfigProfilesInfos)}: ContentPackage {package.Name} is not registered.");
    }

    public Result<ILocalizationsResourcesInfo> GetLocalizationsInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (package is null)
            return FluentResults.Result.Fail($"{nameof(GetLocalizationsInfos)}: ContentPackage is null.");
        
        if (_modInfos.TryGetValue(package, out var result))
        {
            return FluentResults.Result.Ok<ILocalizationsResourcesInfo>(_localizationsConverter.Process(onlySupportedResources?
                result.Localizations.Where(r => 
                    (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                    && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                : result.Localizations
            ));
        }

        return FluentResults.Result.Fail(
            $"{nameof(GetLocalizationsInfos)}: ContentPackage {package.Name} is not registered.");
    }

    public Result<ILuaScriptsResourcesInfo> GetLuaScriptsInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (package is null)
            return FluentResults.Result.Fail($"{nameof(GetLuaScriptsInfos)}: ContentPackage is null.");
        
        if (_modInfos.TryGetValue(package, out var result))
        {
            return FluentResults.Result.Ok<ILuaScriptsResourcesInfo>(_luaScriptsConverter.Process(onlySupportedResources?
                result.LuaScripts.Where(r => 
                    (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                    && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                : result.LuaScripts
            ));
        }

        return FluentResults.Result.Fail(
            $"{nameof(GetLuaScriptsInfos)}: ContentPackage {package.Name} is not registered.");
    }

    public Result<IAssembliesResourcesInfo> GetAssembliesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (packages is null || packages.Count == 0)
            return FluentResults.Result.Fail($"{nameof(GetAssembliesInfos)}: ContentPackage list is null or empty.");
        var builder = ImmutableArray.CreateBuilder<IAssemblyResourceInfo>();
        foreach (var package in packages)
        {
            if (_modInfos.TryGetValue(package, out var result) && result.Assemblies is { IsEmpty: false })
            {
                builder.AddRange(onlySupportedResources?
                    result.Assemblies.Where(r => 
                        (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                        && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                    : result.Assemblies);
            }
        }

        return FluentResults.Result.Ok(_assemblyInfoConverter.Process(builder.MoveToImmutable()));
    }

    public Result<IConfigsResourcesInfo> GetConfigsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (packages is null || packages.Count == 0)
            return FluentResults.Result.Fail($"{nameof(GetConfigsInfos)}: ContentPackage list is null or empty.");
        var builder = ImmutableArray.CreateBuilder<IConfigResourceInfo>();
        foreach (var package in packages)
        {
            if (_modInfos.TryGetValue(package, out var result) && result.Configs is { IsEmpty: false })
            {
                builder.AddRange(onlySupportedResources?
                    result.Configs.Where(r => 
                        (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                        && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                    : result.Configs);
            }
        }

        return FluentResults.Result.Ok(_configsInfoConverter.Process(builder.MoveToImmutable()));
    }

    public Result<IConfigProfilesResourcesInfo> GetConfigProfilesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (packages is null || packages.Count == 0)
            return FluentResults.Result.Fail($"{nameof(GetConfigProfilesInfos)}: ContentPackage list is null or empty.");
        var builder = ImmutableArray.CreateBuilder<IConfigProfileResourceInfo>();
        foreach (var package in packages)
        {
            if (_modInfos.TryGetValue(package, out var result) && result.ConfigProfiles is { IsEmpty: false })
            {
                builder.AddRange(onlySupportedResources?
                    result.ConfigProfiles.Where(r => 
                        (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                        && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                    : result.ConfigProfiles);
            }
        }

        return FluentResults.Result.Ok(_configProfilesConverter.Process(builder.MoveToImmutable()));
    }

    public Result<ILocalizationsResourcesInfo> GetLocalizationsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (packages is null || packages.Count == 0)
            return FluentResults.Result.Fail($"{nameof(GetLocalizationsInfos)}: ContentPackage list is null or empty.");
        var builder = ImmutableArray.CreateBuilder<ILocalizationResourceInfo>();
        foreach (var package in packages)
        {
            if (_modInfos.TryGetValue(package, out var result) && result.Localizations is { IsEmpty: false })
            {
                builder.AddRange(onlySupportedResources?
                    result.Localizations.Where(r => 
                        (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                        && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                    : result.Localizations);
            }
        }

        return FluentResults.Result.Ok(_localizationsConverter.Process(builder.MoveToImmutable()));
    }

    public Result<ILuaScriptsResourcesInfo> GetLuaScriptsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (packages is null || packages.Count == 0)
            return FluentResults.Result.Fail($"{nameof(GetLuaScriptsInfos)}: ContentPackage list is null or empty.");
        var builder = ImmutableArray.CreateBuilder<ILuaScriptResourceInfo>();
        foreach (var package in packages)
        {
            if (_modInfos.TryGetValue(package, out var result) && result.LuaScripts is { IsEmpty: false })
            {
                builder.AddRange(onlySupportedResources?
                    result.LuaScripts.Where(r => 
                        (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                        && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                    : result.LuaScripts);
            }
        }

        return FluentResults.Result.Ok(_luaScriptsConverter.Process(builder.MoveToImmutable()));
    }

    public async Task<Result<IAssembliesResourcesInfo>> GetAssembliesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        return await Task.Run(() => GetAssembliesInfos(packages, onlySupportedResources));
    }

    public async Task<Result<IConfigsResourcesInfo>> GetConfigsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        return await Task.Run(() => GetConfigsInfos(packages, onlySupportedResources));
    }

    public async Task<Result<IConfigProfilesResourcesInfo>> GetConfigProfilesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        return await Task.Run(() => GetConfigProfilesInfos(packages, onlySupportedResources));
    }

    public async Task<Result<ILocalizationsResourcesInfo>> GetLocalizationsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        return await Task.Run(() => GetLocalizationsInfos(packages, onlySupportedResources));
    }

    public async Task<Result<ILuaScriptsResourcesInfo>> GetLuaScriptsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        return await Task.Run(() => GetLuaScriptsInfos(packages, onlySupportedResources));
    }
    
    
}
