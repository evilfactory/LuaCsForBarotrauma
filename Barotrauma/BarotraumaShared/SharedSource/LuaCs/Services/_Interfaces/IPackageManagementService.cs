using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPackageManagementService : IReusableService, IConfigsResourcesInfo, IConfigProfilesResourcesInfo, ILuaScriptsResourcesInfo, IAssembliesResourcesInfo
{
    /// <summary>
    /// Loads and parses the provided <see cref="ContentPackage"/> for <see cref="IResourceInfo"/> supported by the current runtime environment.
    /// Will overwrite any existing package data.
    /// </summary>
    /// <param name="packages">Package to load.</param>
    /// <returns></returns>
    Task<FluentResults.Result> LoadPackageInfosAsync(ContentPackage package);
    /// <summary>
    /// Loads and parses the provided <see cref="ContentPackage"/> collection for <see cref="IResourceInfo"/> supported by the current runtime environment.
    /// Will overwrite any existing package data.
    /// </summary>
    /// <param name="packages">List of packages to load.</param>
    /// <returns></returns>
    Task<IReadOnlyList<(ContentPackage, FluentResults.Result)>> LoadPackagesInfosAsync(IReadOnlyList<ContentPackage> packages);
    IReadOnlyList<ContentPackage> GetAllLoadedPackages();
    bool IsPackageLoaded(ContentPackage package);

    /// <summary>
    /// Filters out resources not suitable for the current environment using the following criteria: <br/>
    /// - Platform (Operating System)<br/>
    /// - Target (Client|Server)<br/>
    /// - Null/Invalid<br/>
    /// - Dependency Package Registered in PMS<br/>
    /// </summary>
    /// <param name="resources"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    ImmutableArray<T> FilterUnloadableResources<T>(IReadOnlyList<T> resources, bool enabledPackagesOnly = false)
        where T : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo;
    void DisposePackageInfos(ContentPackage package);
    void DisposePackagesInfos(IReadOnlyList<ContentPackage> packages);
    FluentResults.Result<IPackageDependency> GetPackageDependencyInfo(ContentPackage ownerPackage, string packageName, ulong steamWorkshopId);
    
    // single
    FluentResults.Result<IAssembliesResourcesInfo> GetAssembliesInfos(ContentPackage package, bool onlySupportedResources = true);
    FluentResults.Result<IConfigsResourcesInfo> GetConfigsInfos(ContentPackage package, bool onlySupportedResources = true);
    FluentResults.Result<IConfigProfilesResourcesInfo> GetConfigProfilesInfos(ContentPackage package, bool onlySupportedResources = true);
    FluentResults.Result<ILuaScriptsResourcesInfo> GetLuaScriptsInfos(ContentPackage package, bool onlySupportedResources = true);
    // collection
    FluentResults.Result<IAssembliesResourcesInfo> GetAssembliesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    FluentResults.Result<IConfigsResourcesInfo> GetConfigsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    FluentResults.Result<IConfigProfilesResourcesInfo> GetConfigProfilesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
  FluentResults.Result<ILuaScriptsResourcesInfo> GetLuaScriptsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    
    Task<FluentResults.Result<IAssembliesResourcesInfo>> GetAssembliesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    Task<FluentResults.Result<IConfigsResourcesInfo>> GetConfigsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
    Task<FluentResults.Result<IConfigProfilesResourcesInfo>> GetConfigProfilesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
  Task<FluentResults.Result<ILuaScriptsResourcesInfo>> GetLuaScriptsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true);
}
