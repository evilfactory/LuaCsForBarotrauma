using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
using Barotrauma.Steam;
using FluentResults;
using FluentResults.LuaCs;
using QuikGraph;

namespace Barotrauma.LuaCs.Services;

public class PackageManagementService : IPackageManagementService
{
    private readonly Func<IPackageService> _contentPackageServiceFactory;
    private readonly Lazy<IAssemblyManagementService> _assemblyManagementService;
    private readonly ConcurrentDictionary<ContentPackage, IPackageService> _contentPackages = new();
    private readonly ConcurrentQueue<LoadablePackage> _queuedPackages = new();
    private readonly ConcurrentDictionary<OneOf.OneOf<ContentPackage, string, ulong>, IPackageDependencyInfo> _packageDependencyInfos = new();

    /// <summary>
    /// ConcurrentDictionary handles access synchronization. This is to ensure that we are not trying to
    /// load/unload/modify the collection from multiple threads.
    /// </summary>
    private readonly ReaderWriterLockSlim _contentPackagesModificationsLock = new();
    
    public PackageManagementService(
        Func<IPackageService> getPackageService,
        Lazy<IAssemblyManagementService> assemblyManagementService)
    {
        this._contentPackageServiceFactory = getPackageService;
        this._assemblyManagementService = assemblyManagementService;
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }

    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }

    public bool IsAssemblyLoadedGlobal(string friendlyName)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result QueuePackages(ImmutableArray<LoadablePackage> packages)
    {
        _contentPackagesModificationsLock.EnterWriteLock();
        try
        {
            foreach (LoadablePackage package in packages)
            {
                _queuedPackages.Enqueue(package);
            }
            return FluentResults.Result.Ok();
        }
        finally
        {
            _contentPackagesModificationsLock.ExitWriteLock();
        }
    }

    public FluentResults.Result ProcessQueuedPackages(bool rescanPackages = false, bool loadParallel = true, bool reportFailOnDuplicates = false)
    {
        if (!ModUtils.Environment.IsMainThread)
            throw new InvalidOperationException($"{nameof(ProcessQueuedPackages)}: This method can only be called on the main thread.");
        
        _contentPackagesModificationsLock.EnterReadLock();
        try
        {
            if (_queuedPackages.IsEmpty)
            {
                return FluentResults.Result.Ok()
                    .WithSuccess($"{nameof(ProcessQueuedPackages)}: The Queue is empty.");
            }
            
            ConcurrentStack<IError> errors = new();
            ConcurrentStack<ISuccess> successes = new();
            
            // Load ModConfigInfo
            try
            {
                
                FluentResults.Result res = new FluentResults.Result();
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                if (loadParallel)
                {
                    var ret = Parallel.ForEach(_queuedPackages, package =>
                    {
                        var r = LoadPackageInfo(package);
                        if (r.IsFailed)
                        {
                            errors.Push(new Error(
                                $"{nameof(ProcessQueuedPackages)}: Package {package.Package?.Name} failed to load."));
                            errors.PushRange(r.Errors.ToArray());
                        }
                        else
                        {
                            successes.Push(new Success($"Successfully loaded Package Info for {package.Package?.Name}"));
                        }
                    });
                }
                else
                {
                    _queuedPackages.ForEach(package =>
                    {
                        var r = LoadPackageInfo(package);
                        if (r.IsFailed)
                        {
                            errors.Push(new Error($"{nameof(ProcessQueuedPackages)}: Package {package.Package?.Name} failed to load."));
                            errors.PushRange(r.Errors.ToArray());
                        }
                        else
                        {
                            successes.Push(new Success($"Successfully loaded Package Info for {package.Package?.Name}"));
                        }
                    });
                }
                stopwatch.Stop();
                res = res.WithReason(new Success($"Completed ModConfigInfo loading in {stopwatch.Elapsed}."))
                    .WithSuccesses(successes)
                    .WithErrors(errors);
                
                successes.Clear();
                errors.Clear();
                stopwatch.Reset();
                
                // Sort all resources by dependencies, check for errors, then:
                // load localizations
                // load assemblies and cs scripts, no type init.
                // register types as needed for resolutions
                // load configs and profiles
                // load styles
                // init types/plugins
                // register types for events
                // load lua scripts
            }
            catch (AggregateException ae)
            {
                return FluentResults.Result.Fail(new Error($"{nameof(ProcessQueuedPackages)}: Failed to load packages! AE.")
                    .WithMetadata(MetadataType.ExceptionDetails, ae.InnerException?.Message ?? ae.Message)
                    .WithMetadata(MetadataType.StackTrace, ae.StackTrace)
                    .WithMetadata(MetadataType.ExceptionObject, this));
            }
            catch (ArgumentNullException ane)
            {
                return FluentResults.Result.Fail(new Error($"{nameof(ProcessQueuedPackages)}: Failed to load packages! ANE.")
                    .WithMetadata(MetadataType.ExceptionDetails, ane.InnerException?.Message ?? ane.Message)
                    .WithMetadata(MetadataType.StackTrace, ane.StackTrace)
                    .WithMetadata(MetadataType.ExceptionObject, this));
            }
            
            return FluentResults.Result.Ok();

            
            /*
             * Helper functions
             */
            
            // register in the list so we can check against it.
            FluentResults.Result LoadPackageInfo(LoadablePackage package)
            {
                try
                {
                    if (_contentPackages.ContainsKey(package.Package))
                    {
                        if (reportFailOnDuplicates)
                        {
                            return FluentResults.Result.Fail(new Error($"The package {package.Package?.Name} is already loaded.")
                                .WithMetadata(MetadataType.ExceptionObject, this)
                                .WithMetadata(MetadataType.RootObject, package.Package));
                        }
                        return FluentResults.Result.Ok();
                    }
                    if (package.Package == null)
                    {
                        return FluentResults.Result.Fail(new Error($"{nameof(LoadPackageInfo)}: Package is null!")
                            .WithMetadata(MetadataType.ExceptionObject, this)
                            .WithMetadata(MetadataType.RootObject, package));
                    }

                    return _contentPackages[package.Package].LoadResourcesInfo(package);
                }
                catch (NullReferenceException nre)
                {
                    return FluentResults.Result.Fail(new Error($"{nameof(LoadPackageInfo)}: NRE while loading package {package.Package?.Name}!")
                        .WithMetadata(MetadataType.ExceptionObject, this)
                        .WithMetadata(MetadataType.StackTrace, nre.StackTrace ?? "StackTrace not available")
                        .WithMetadata(MetadataType.ExceptionDetails, nre.InnerException?.Message ?? nre.Message)
                        .WithMetadata(MetadataType.RootObject, package));
                }
            }

            /*
             * Return array: (Normal, MissingDepsRes, MissingDeps)
             */
            FluentResults.Result<(ImmutableArray<T>, ImmutableArray<T>, ImmutableArray<IPackageDependencyInfo>)> GetLoadablePackages<T>(ImmutableArray<T> resources, bool errorForPacksMissingDeps = false)
                where T : class, IPackageDependenciesInfo, IPackageInfo, IResourceInfo, IResourceCultureInfo
            {
                
                // filter optional resources (process later)
                // add back in optional packages that are required by other required packages
                // filter and log required packages that are missing dependencies
                // re-include optionals where dependencies are available
                // return both lists (A normal, B missingDeps).

                HashSet<IPackageDependencyInfo> missingDeps = new();
                var missingDepsBuilder = ImmutableArray.CreateBuilder<T>();
                
                var reqPacks = resources.Where(r => !r.Optional).Select(r => r.OwnerPackage).Distinct().ToImmutableHashSet();
                var optPack = resources.Where(r => r.Optional).Select(r => r.OwnerPackage).Distinct().ToImmutableHashSet();
                var req = resources
                    .Where(r => !r.Optional)
                    .Where(CheckEnvironmentSupported)
                    .Where(r =>
                    {
                        if (r.Dependencies.Length == 0)
                            return true;

                        if (CheckDependenciesLoaded(r.Dependencies, out var missingDepsList)) 
                            return true;
                        
                        missingDepsBuilder.Add(r);
                        missingDeps.UnionWith(missingDepsList);
                        return false;
                    });
                var reqOptionals = resources.Where(r => r.Optional && optPack.Contains(r.OwnerPackage));
                var notReqOptionals = resources.Where(r => r.Optional && !optPack.Contains(r.OwnerPackage));
                
                throw new NotImplementedException();
            }

            FluentResults.Result<ImmutableArray<T>> SortByDependencies<T>(ImmutableArray<T> resources)
                where T : class, IPackageDependenciesInfo, IPackageInfo, IResourceInfo
            {
                throw new NotImplementedException();
                // construct node-dependencies array
                // add to nodes to graph
                // add edges (deps) to graph
                // see if acyclic
                    // log errors if not
                // return resulting array
            }
            
        }
        finally
        {
            _contentPackagesModificationsLock.ExitReadLock();
        }
    }

    public FluentResults.Result UnloadPackages()
    {
        if (!ModUtils.Environment.IsMainThread)
        {
            return FluentResults.Result.Fail(
                new ExceptionalError(new InvalidOperationException($"{nameof(UnloadPackages)}: This method can only be called on the main thread."))
                    .WithMetadata(MetadataType.ExceptionObject, this));
        }

        var res = new FluentResults.Result();
        _contentPackagesModificationsLock.EnterWriteLock();
        try
        {
            
        }
        finally
        {
            _contentPackagesModificationsLock.ExitWriteLock();
        }

        throw new NotImplementedException();
    }

    public bool IsPackageLoaded(ContentPackage package) => package is not null && _contentPackages.ContainsKey(package);

    public bool CheckDependencyLoaded(IPackageDependencyInfo info) =>
        info is not null && IsPackageLoaded(info.DependencyPackage);

    public bool CheckDependenciesLoaded([NotNull]IEnumerable<IPackageDependencyInfo> infos, out ImmutableArray<IPackageDependencyInfo> missingPackages)
    {
        var missing = ImmutableArray.CreateBuilder<IPackageDependencyInfo>();
        missing.AddRange(infos
            .Where(i => i.DependencyPackage is not null)
            .DistinctBy(i => i.DependencyPackage)
            .Where(i => !CheckDependencyLoaded(i)));
        missingPackages = missing.MoveToImmutable();
        return missingPackages.Length == 0;
    }
    
    public bool CheckEnvironmentSupported(IPlatformInfo platform)
    {
        return (platform.SupportedPlatforms & ModUtils.Environment.CurrentPlatform) > 0
            && (platform.SupportedTargets & ModUtils.Environment.CurrentTarget) > 0;
    }

    public Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(ContentPackage package)
    {
        if (package is null)
            return new FluentResults.Result<IPackageDependencyInfo>()
                .WithError(new Error($"{nameof(GetPackageDependencyInfoRecord)}: Package is null!")
                    .WithMetadata(MetadataType.ExceptionObject, this));
        if (!_packageDependencyInfos.TryGetValue(package, out var info))
            return AddDependencyRecord(package, package.Name, package.Path, package.TryExtractSteamWorkshopId(out var id) ? id.Value : 0, id != null);
        return new Result<IPackageDependencyInfo>()
            .WithValue(info)
            .WithSuccess($"Existing value.");
    }

    public Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(ulong steamWorkshopId)
    {
        throw new NotImplementedException();
    }

    public Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(string packageName)
    {
        throw new NotImplementedException();
    }

    public IPackageDependencyInfo CreateMissingPackageDependencyInfoRecord(
        string packageName,
        string packagePath,
        ulong steamWorkshopId)
    {
        return new DependencyInfo()
        {
            DependencyPackage = null,
            FallbackPackageName = packageName,
            FolderPath = packagePath.IsNullOrWhiteSpace() ? null : System.IO.Path.GetFullPath(packagePath),
            SteamWorkshopId = steamWorkshopId,
            IsMissing = true,
            IsWorkshopInstallation = false
        };
    }

    private Result<IPackageDependencyInfo> AddDependencyRecord(
        ContentPackage package,
        string packageName,
        string folderPath,
        ulong steamWorkshopId,
        bool isMissing)
    {
        try
        {
            var dependencyInfo = new DependencyInfo()
            {
                DependencyPackage = package,
                FallbackPackageName = packageName,
                FolderPath = System.IO.Path.GetFullPath(folderPath),
                SteamWorkshopId = steamWorkshopId,
                IsMissing = isMissing,
                IsWorkshopInstallation = steamWorkshopId != 0
            };
            if (package is not null)
            {
                _packageDependencyInfos.AddOrUpdate(package, pack => dependencyInfo,
                (pack, dep) => dependencyInfo);
            }
            return new FluentResults.Result<IPackageDependencyInfo>()
                .WithValue(dependencyInfo)
                .WithSuccess($"New value created.");
        }
        catch (Exception ex)
        {
            return new FluentResults.Result<IPackageDependencyInfo>()
                .WithError(new ExceptionalError(ex)
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.ExceptionDetails, ex.Message)
                    .WithMetadata(MetadataType.RootObject, package)
                    .WithMetadata(MetadataType.StackTrace, ex.StackTrace ?? "StackTrace not available"));
        }
    }
}
