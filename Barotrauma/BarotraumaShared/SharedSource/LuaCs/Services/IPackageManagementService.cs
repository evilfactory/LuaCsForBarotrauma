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
    /// <returns></returns>
    FluentResults.Result QueuePackages(ImmutableArray<LoadablePackage> packages);
    
    /// <summary>
    /// Loads queued packages, skips already loaded packages.
    /// </summary>
    /// <param name="rescanPackages"></param>
    /// <param name="loadParallel"></param>
    /// <param name="reportFailOnDuplicates"></param>
    /// <returns></returns>
    FluentResults.Result ProcessQueuedPackages(bool rescanPackages = false, bool loadParallel = true, bool reportFailOnDuplicates = false);
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

    public IPackageDependencyInfo CreateMissingPackageDependencyInfoRecord(string packageName, 
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
