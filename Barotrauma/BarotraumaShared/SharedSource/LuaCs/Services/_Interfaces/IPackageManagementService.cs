using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPackageManagementService : IReusableService
{
    /// <summary>
    /// Adds packages to the queue of loadable packages without initializing them.
    /// </summary>
    /// <param name="packages"></param>
    void QueuePackages(ImmutableArray<LoadablePackage> packages);
    
    /// <summary>
    /// Generates the ModConfigInfo for all queued packages and adds them to the store.
    /// </summary>
    /// <param name="loadParallel">Use multithreaded loading.</param>
    /// <param name="reportFailOnDuplicates">Whether duplicate packages should be reported as errors.</param>
    /// <returns>Failure/Success records for each package.</returns>
    FluentResults.Result ParseQueuedPackages(bool loadParallel = true, bool reportFailOnDuplicates = false);
    /// <summary>
    /// Loads only the localizations, configs, and config profiles for stored packages. 
    /// </summary>
    /// <param name="loadParallel"></param>
    /// <returns></returns>
    FluentResults.Result LoadPackageConfigsResourcesGroup(bool loadParallel = true);
    /// <summary>
    /// Loads all resources for stored packages.
    /// </summary>
    /// <param name="loadParallel">Use multithreaded loading.</param>
    /// <param name="safeResourcesOnly">Only load safe scripting resources, such as Lua. C# plugins disabled.</param>
    /// <returns></returns>
    FluentResults.Result LoadAllPackageResources(bool loadParallel = true, bool safeResourcesOnly = true);
    FluentResults.Result UnloadPackages();
    bool IsPackageLoaded(ContentPackage package);
    bool CheckDependencyLoaded(IPackageDependencyInfo info);
    bool CheckDependenciesLoaded([NotNull]IEnumerable<IPackageDependencyInfo> infos, out ImmutableArray<IPackageDependencyInfo> missingPackages);
    bool CheckEnvironmentSupported(IPlatformInfo platform);
    /// <summary>
    /// Tries to get the package dependency record to refer to that specific package if it exists, optionally create it.
    /// </summary>
    /// <param name="package">ContentPackage reference</param>
    /// <param name="addIfMissing">Register a new IPackageDependencyInfo reference.</param>
    /// <returns></returns>
    FluentResults.Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(ContentPackage package, 
        bool addIfMissing = false);
    /// <summary>
    /// Tries to get the package dependency record to refer to that specific package if it exists, optionally create it.
    /// </summary>
    /// <param name="steamWorkshopId">The Steam Workshop ID, if available, if not enter zero ('0').</param>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="folderPath">The folder path, as formatted in [ContentPackage.Path].</param>
    /// <param name="addIfMissing">Register a new IPackageDependencyInfo reference.</param>
    /// <returns></returns>
    FluentResults.Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(ulong steamWorkshopId, 
        string packageName, string folderPath = null, bool addIfMissing = false);
    /// <summary>
    /// Tries to get the package dependency record to refer to that specific package if it exists.
    /// Note: This overload does not allow the registration of a new dependency.
    /// </summary>
    /// <param name="folderPath">The folder path, as formatted in [ContentPackage.Path].</param>
    /// <returns></returns>
    FluentResults.Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(string folderPath);

    IPackageDependencyInfo CreateOrphanPackageDependencyInfoRecord(string packageName, 
        string packagePath, ulong steamWorkshopId);
}

public readonly record struct LoadablePackage
{
    public ContentPackage Package { get; }
    public bool IsEnabled { get; }

    public LoadablePackage(ContentPackage package, bool isEnabled)
    {
        Package = package;
        IsEnabled = isEnabled;
    }
    
    public static ImmutableArray<LoadablePackage> FromEnumerable(IEnumerable<ContentPackage> packages, bool isEnabled)
    {
        var builder = ImmutableArray.CreateBuilder<LoadablePackage>();
        packages.ForEach(p => builder.Add(new LoadablePackage(p, isEnabled)));
        return builder.ToImmutable();
    }
}
