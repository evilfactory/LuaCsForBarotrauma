using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;
using FluentResults;
// ReSharper disable UseCollectionExpression

namespace Barotrauma.LuaCs.Services;

public partial class PackageManagementService : IPackageManagementService
{
    public PackageManagementService(
        IConverterServiceAsync<ContentPackage, IModConfigInfo> modConfigParserService, 
        IProcessorService<IReadOnlyList<IAssemblyResourceInfo>, IAssembliesResourcesInfo> assemblyInfoConverter, 
        IProcessorService<IReadOnlyList<IConfigResourceInfo>, IConfigsResourcesInfo> configsInfoConverter, 
        IProcessorService<IReadOnlyList<IConfigProfileResourceInfo>, IConfigProfilesResourcesInfo> configProfilesConverter, 
        IProcessorService<IReadOnlyList<ILocalizationResourceInfo>, ILocalizationsResourcesInfo> localizationsConverter, 
        IProcessorService<IReadOnlyList<ILuaScriptResourceInfo>, ILuaScriptsResourcesInfo> luaScriptsConverter, 
        IPackageInfoLookupService packageInfoLookupService, Func<IReadOnlyList<IStylesResourceInfo>, IStylesResourcesInfo> stylesInfoConverter)
    {
        _stylesInfoConverter = stylesInfoConverter;
        _modConfigParserService = modConfigParserService;
        _assemblyInfoConverter = assemblyInfoConverter;
        _configsInfoConverter = configsInfoConverter;
        _configProfilesConverter = configProfilesConverter;
        _localizationsConverter = localizationsConverter;
        _luaScriptsConverter = luaScriptsConverter;
        _packageInfoLookupService = packageInfoLookupService;
    }
    
    private readonly Func<IReadOnlyList<IStylesResourceInfo>, IStylesResourcesInfo> _stylesInfoConverter;
    
    public ImmutableArray<IStylesResourceInfo> Styles => _modInfos.IsEmpty ? ImmutableArray<IStylesResourceInfo>.Empty 
    : _modInfos.SelectMany(kvp => kvp.Value.Styles).ToImmutableArray();

    public Result<IStylesResourcesInfo> GetStylesInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (package is null)
            return FluentResults.Result.Fail($"{nameof(GetStylesInfos)}: ContentPackage is null.");
        if (_modInfos.TryGetValue(package, out var result))
            return FluentResults.Result.Ok<IStylesResourcesInfo>(_stylesInfoConverter(onlySupportedResources?
                result.Styles.Where(r => 
                    (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                    && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                : result.Styles
            ));
        return FluentResults.Result.Fail(
            $"{nameof(GetStylesInfos)}: ContentPackage {package.Name} is not registered.");
    }

    public Result<IStylesResourcesInfo> GetStylesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        ((IService)this).CheckDisposed();
        if (packages is null || packages.Count == 0)
            return FluentResults.Result.Fail($"{nameof(GetStylesInfos)}: ContentPackage list is null or empty.");
        var builder = ImmutableArray.CreateBuilder<IStylesResourceInfo>();
        foreach (var package in packages)
        {
            if (_modInfos.TryGetValue(package, out var result) && result.Styles is { IsEmpty: false })
            {
                builder.AddRange(onlySupportedResources?
                    result.Styles.Where(r => 
                        (r.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
                        && (r.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0).ToImmutableArray()
                    : result.Styles);
            }
        }

        return FluentResults.Result.Ok(_stylesInfoConverter(builder.MoveToImmutable()));
    }

    public async Task<Result<IStylesResourcesInfo>> GetStylesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        return await Task.Run(() => GetStylesInfos(packages, onlySupportedResources));

    }
}
