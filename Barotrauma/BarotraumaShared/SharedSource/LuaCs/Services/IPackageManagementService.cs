using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    FluentResults.Result UnloadPackages(bool errorOnFailures = true);
    bool IsPackageLoaded(ContentPackage package);
    bool CheckDependencyLoaded(IPackageDependencyInfo info);
    bool CheckDependenciesLoaded(IEnumerable<IPackageDependencyInfo> infos, out IReadOnlyList<IPackageDependencyInfo> missingPackages);
    bool CheckEnvironmentSupported(IPlatformInfo platform);
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
