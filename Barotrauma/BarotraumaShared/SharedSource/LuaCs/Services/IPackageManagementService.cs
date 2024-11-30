using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPackageManagementService : IService
{
    /// <summary>
    /// Adds packages to the queue of loadable packages without initializing them.
    /// </summary>
    /// <param name="packages"></param>
    void QueuePackages(ImmutableArray<LoadablePackage> packages);
    
    /// <summary>
    /// Generates the ModConfigInfo for all queued packages and then loads them.
    /// </summary>
    /// <param name="rescanPackages">Whether duplicate queued packages that are already prepared should be discarded and regenerated.</param>
    /// <param name="loadParallel">Use multithreaded loading.</param>
    /// <param name="reportFailOnDuplicates">Whether duplicate packages should be reported as errors.</param>
    /// <returns>Failure/Success records for each package.</returns>
    FluentResults.Result LoadQueuedPackages(bool rescanPackages = false, bool loadParallel = true, 
        bool reportFailOnDuplicates = false);
    FluentResults.Result UnloadPackages();
    bool IsPackageLoaded(ContentPackage package);
    bool CheckDependencyLoaded(IPackageDependencyInfo info);
    bool CheckDependenciesLoaded([NotNull]IEnumerable<IPackageDependencyInfo> infos, out ImmutableArray<IPackageDependencyInfo> missingPackages);
    bool CheckEnvironmentSupported(IPlatformInfo platform);
    /// <summary>
    /// Gets or creates a package dependency record to refer to that specific package.
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    FluentResults.Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(ContentPackage package);
    
    /// <summary>
    /// Gets or creates a package dependency record to refer to that specific package.
    /// </summary>
    /// <param name="steamWorkshopId"></param>
    /// <returns></returns>
    FluentResults.Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(ulong steamWorkshopId);

    FluentResults.Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(string packageName);

    public IPackageDependencyInfo CreateOrphanPackageDependencyInfoRecord(string packageName, 
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
