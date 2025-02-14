using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPackageManagementService : IReusableService, ILocalizationsResourcesInfo, IConfigsResourcesInfo, IConfigProfilesResourcesInfo, ILuaScriptsResourcesInfo, IAssembliesResourcesInfo
#if CLIENT
    ,IStylesResourcesInfo
#endif

{
    /// <summary>
    /// Loads and parses the provided <see cref="ContentPackage"/> for <see cref="IResourceInfo"/> supported by the current runtime environment.
    /// </summary>
    /// <param name="packages"></param>
    /// <returns></returns>
    Task<FluentResults.Result> LoadPackageInfosAsync(ContentPackage packages);
    /// <summary>
    /// Loads and parses the provided <see cref="ContentPackage"/> collection for <see cref="IResourceInfo"/> supported by the current runtime environment.
    /// </summary>
    /// <param name="packages"></param>
    /// <returns></returns>
    Task<IReadOnlyList<(ContentPackage, FluentResults.Result)>> LoadPackagesInfosAsync(IReadOnlyList<ContentPackage> packages);
    IReadOnlyList<ContentPackage> GetAllLoadedPackages();
    void DisposePackageInfos(ContentPackage package);
    void DisposePackagesInfos(IReadOnlyList<ContentPackage> packages);
    void DisposeAllPackagesInfos();
    
    // single
    FluentResults.Result<IAssembliesResourcesInfo> GetAssembliesInfos(ContentPackage package, bool onlySupportedResources = true);
    FluentResults.Result<IConfigsResourcesInfo> GetConfigsInfos(ContentPackage package, bool onlySupportedResources = true);
    FluentResults.Result<IConfigProfilesResourcesInfo> GetConfigProfilesInfos(ContentPackage package, bool onlySupportedResources = true);
    FluentResults.Result<ILocalizationsResourcesInfo> GetLocalizationsInfos(ContentPackage package, bool onlySupportedResources = true);
    FluentResults.Result<ILuaScriptsResourcesInfo> GetLuaScriptsInfos(ContentPackage package, bool onlySupportedResources = true);
#if CLIENT
    FluentResults.Result<IStylesResourcesInfo> GetStylesInfos(ContentPackage package, bool onlySupportedResources = true);
#endif
    // collection
    FluentResults.Result<IAssembliesResourcesInfo> GetAssembliesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    FluentResults.Result<IConfigsResourcesInfo> GetConfigsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    FluentResults.Result<IConfigProfilesResourcesInfo> GetConfigProfilesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    FluentResults.Result<ILocalizationsResourcesInfo> GetLocalizationsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    FluentResults.Result<ILuaScriptsResourcesInfo> GetLuaScriptsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
#if CLIENT
    FluentResults.Result<IStylesResourcesInfo> GetStylesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
#endif
    
    Task<FluentResults.Result<IAssembliesResourcesInfo>> GetAssembliesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    Task<FluentResults.Result<IConfigsResourcesInfo>> GetConfigsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    Task<FluentResults.Result<IConfigProfilesResourcesInfo>> GetConfigProfilesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    Task<FluentResults.Result<ILocalizationsResourcesInfo>> GetLocalizationsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    Task<FluentResults.Result<ILuaScriptsResourcesInfo>> GetLuaScriptsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
#if CLIENT
    Task<FluentResults.Result<IStylesResourcesInfo>> GetStylesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
#endif
    
}
