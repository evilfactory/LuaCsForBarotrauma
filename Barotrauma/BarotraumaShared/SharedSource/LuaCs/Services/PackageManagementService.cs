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
    private readonly ConcurrentDictionary<DependencyEntryKey, IPackageDependencyInfo> _packageDependencyInfos = new();

    /// <summary>
    /// ConcurrentDictionary handles access/read synchronization. This is to ensure that we are not trying to
    /// access the collection during a load/unload/modify operation.
    /// </summary>
    private readonly ReaderWriterLockSlim _contentPackagesModificationsLock = new();
    /// <summary>
    /// This lock ensures that we are not adding new entries to the queue between when we read the contents and
    /// empty the buffer. 
    /// </summary>
    private readonly ReaderWriterLockSlim _packageQueueProcessingLock = new();
    
    public PackageManagementService(
        Func<IPackageService> getPackageService,
        Lazy<IAssemblyManagementService> assemblyManagementService)
    {
        this._contentPackageServiceFactory = getPackageService;
        this._assemblyManagementService = assemblyManagementService;
    }

    #region STATE_RESET

    public void Dispose()
    {
        // TODO release managed resources here
    }

    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }

    #endregion

    public void QueuePackages(ImmutableArray<LoadablePackage> packages)
    {
        _packageQueueProcessingLock.EnterReadLock();
        try
        {
            foreach (LoadablePackage package in packages) 
                _queuedPackages.Enqueue(package);
        }
        finally
        {
            _packageQueueProcessingLock.ExitReadLock();
        }
    }

    public FluentResults.Result ParseQueuedPackages(bool loadParallel = true, bool reportFailOnDuplicates = false)
    {
        if (!ModUtils.Environment.IsMainThread)
            throw new InvalidOperationException($"{nameof(ParseQueuedPackages)}: This method can only be called on the main thread.");
        
        ImmutableArray<LoadablePackage> packagesToProcess = ImmutableArray<LoadablePackage>.Empty;
        
        _packageQueueProcessingLock.EnterWriteLock();
        try
        {
            Interlocked.MemoryBarrier();
            if (_queuedPackages.IsEmpty)
                return FluentResults.Result.Ok().WithSuccess($"{nameof(ParseQueuedPackages)}: The Queue is empty.");
            packagesToProcess = _queuedPackages.Where(p => p.Package is not null)
                .Distinct().ToImmutableArray();
            _queuedPackages.Clear();
        }
        finally
        {
            _packageQueueProcessingLock.ExitWriteLock();
        }
        
        FluentResults.Result[] loadResults = new FluentResults.Result[packagesToProcess.Length];
        FluentResults.Result res = new FluentResults.Result();
        
        // Load ModConfigInfo
        _contentPackagesModificationsLock.EnterWriteLock();
        try
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
           
            Interlocked.MemoryBarrier();
            if (loadParallel)
            {
                Parallel.For(0, loadResults.Length, new ParallelOptions()
                {
                    /*
                     * This is an IO-bound operation. The purpose of parallelism here is to allow loaded package
                     * data to be processed while another package is waiting on the storage device for its info.
                     */
                    MaxDegreeOfParallelism = 2
                },i =>
                {
                    loadResults[i] = LoadPackageInfo(packagesToProcess[i]);
                });
            }
            else
            {
                for (int i = 0; i < loadResults.Length; i++)
                {   
                    loadResults[i] = LoadPackageInfo(packagesToProcess[i]);
                }
            }
            
            stopwatch.Stop();

            res.WithSuccess(new Success(
                $"Completed parsing of {loadResults.Length} packages in {stopwatch.ElapsedMilliseconds} milliseconds."));
            
            for (int i = 0; i < loadResults.Length; i++)
            {
                res = loadResults[i].IsSuccess 
                    ? res.WithSuccesses(loadResults[i].Successes) 
                    : res.WithErrors(loadResults[i].Errors);
            }
            
            return res;
        }
        catch (AggregateException ae)
        {
            return FluentResults.Result.Fail(new Error($"{nameof(ParseQueuedPackages)}: Failed to load packages! AE.")
                .WithMetadata(MetadataType.ExceptionDetails, ae.InnerException?.Message ?? ae.Message)
                .WithMetadata(MetadataType.StackTrace, ae.StackTrace)
                .WithMetadata(MetadataType.ExceptionObject, this));
        }
        catch (ArgumentNullException ane)
        {
            return FluentResults.Result.Fail(
                new Error($"{nameof(ParseQueuedPackages)}: Failed to load packages! ANE.")
                    .WithMetadata(MetadataType.ExceptionDetails, ane.InnerException?.Message ?? ane.Message)
                    .WithMetadata(MetadataType.StackTrace, ane.StackTrace)
                    .WithMetadata(MetadataType.ExceptionObject, this));
        }
        finally
        {
            _contentPackagesModificationsLock.ExitWriteLock();
        }

        
        /*
         * Helper functions
         */
        
        // register in the list so we can check against it.
        FluentResults.Result LoadPackageInfo(LoadablePackage package)
        {
            try
            {
                if (package.Package == null)
                {
                     return FluentResults.Result.Fail(
                         new Error($"{nameof(LoadPackageInfo)}: Package is null!")
                         .WithMetadata(MetadataType.ExceptionObject, this)
                         .WithMetadata(MetadataType.RootObject, package));
                }

                if (_contentPackages.TryGetValue(package.Package, out var packageService))
                {
                    if (reportFailOnDuplicates)
                    {
                        return FluentResults.Result.Fail(new Error($"The package {package.Package?.Name} is already loaded.")
                            .WithMetadata(MetadataType.ExceptionObject, this)
                            .WithMetadata(MetadataType.RootObject, package.Package));
                    }

                    return FluentResults.Result.Ok();
                }
                
                packageService = _contentPackageServiceFactory.Invoke();
                _contentPackages[package.Package] = packageService;
                return packageService.LoadResourcesInfo(package);
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
    }

    public FluentResults.Result LoadPackageConfigsResourcesGroup(bool loadParallel = true)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result LoadAllPackageResources(bool loadParallel = true, bool safeResourcesOnly = true)
    {
        throw new NotImplementedException();
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
            // TODO: Finish him
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

    public Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(ContentPackage package, bool addIfMissing = false)
    {
        if (package is null)
        {
            return new FluentResults.Result<IPackageDependencyInfo>()
                .WithError(new Error($"{nameof(GetPackageDependencyInfoRecord)}: Package is null!")
                    .WithMetadata(MetadataType.ExceptionObject, this));
        }

        if (_packageDependencyInfos.TryGetValue(package, out var result))
        {
            return new FluentResults.Result<IPackageDependencyInfo>()
                .WithValue(result);
        }
        
        if (addIfMissing)
        {
            return AddDependencyRecord(package, package.Name, package.Path, 
                package.TryExtractSteamWorkshopId(out var id) ? id.Value : 0,
                false);
        }

        return FluentResults.Result.Fail<IPackageDependencyInfo>(new Error($"Could not find package {package.Name}!")
            .WithMetadata(MetadataType.ExceptionObject, this)
            .WithMetadata(MetadataType.RootObject, package));
    }

    public Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(ulong steamWorkshopId, string packageName, string folderPath = null,
        bool addIfMissing = false)
    {
        if (packageName.IsNullOrWhiteSpace() || folderPath.IsNullOrWhiteSpace())
        {
            return new FluentResults.Result<IPackageDependencyInfo>()
                .WithError(new Error($"{nameof(GetPackageDependencyInfoRecord)}: folder path and/or package name are null!")
                    .WithMetadata(MetadataType.ExceptionObject, this));
        }

        if (_packageDependencyInfos.TryGetValue((packageName,steamWorkshopId,folderPath), out var result))
        {
            return new FluentResults.Result<IPackageDependencyInfo>()
                .WithValue(result);
        }

        // TODO: Finish this
        throw new NotImplementedException();
    }

    public Result<IPackageDependencyInfo> GetPackageDependencyInfoRecord(string folderPath)
    {
        throw new NotImplementedException();
    }


    public IPackageDependencyInfo CreateOrphanPackageDependencyInfoRecord(
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
        // TODO: Redo
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

    private readonly record struct DependencyEntryKey : IEqualityComparer<DependencyEntryKey>, IEquatable<DependencyEntryKey>
    {
        public ContentPackage Package { get; init; }
        public string FolderPath { get; init; }
        public string PackageName { get; init; }
        public ulong SteamWorkshopId { get; init; }

        public DependencyEntryKey(ContentPackage package)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package), $"{nameof(DependencyEntryKey)}.ctor: Package cannot be null!");
            PackageName = package.Name;
            SteamWorkshopId = package.TryExtractSteamWorkshopId(out var id) ? id.Value : (ulong)0;
            FolderPath = package.Path;
        }

        public DependencyEntryKey(string packageName, string folderPath, ulong steamWorkshopId)
        {
            PackageName = packageName;
            SteamWorkshopId = steamWorkshopId;
            FolderPath = folderPath;
            Package = null;
        }

        public DependencyEntryKey(string packageName, ulong steamWorkshopId)
        {
            PackageName = packageName;
            SteamWorkshopId = steamWorkshopId;
            FolderPath = null;
            Package = null;
        }

        public bool Equals(DependencyEntryKey other)
        {
            return Equals(this, other);
        }

        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        public bool Equals(DependencyEntryKey x, DependencyEntryKey y)
        {
            if (x == y)
                return true;
            
            if (x.Package is not null && y.Package is not null && x.Package == Package)
                return true;

            // folder should be a unique key if not unset.
            if (!x.FolderPath.IsNullOrWhiteSpace() && !y.FolderPath.IsNullOrWhiteSpace() &&
                x.FolderPath == FolderPath)
                return true;

            if (!x.PackageName.IsNullOrWhiteSpace() && !y.PackageName.IsNullOrWhiteSpace() 
                                                    && x.SteamWorkshopId != 0 && y.SteamWorkshopId != 0)
                return x.PackageName == y.PackageName && x.SteamWorkshopId == y.SteamWorkshopId;

            if (!x.PackageName.IsNullOrWhiteSpace() && !y.PackageName.IsNullOrWhiteSpace() && x.PackageName == PackageName)
                return true;

            if (x.SteamWorkshopId != 0 && y.SteamWorkshopId != 0 &&
                x.SteamWorkshopId == y.SteamWorkshopId)
                return true;

            return false;
        }

        public int GetHashCode(DependencyEntryKey obj)
        {
            if (!obj.PackageName.IsNullOrWhiteSpace())
                return obj.PackageName.GetHashCode();
            if (obj.SteamWorkshopId != 0)
                return obj.SteamWorkshopId.GetHashCode();
            if (obj.Package is not null)
                return obj.Package.GetHashCode();
            // We don't want to check the FolderPath because we want to resolve dependencies using packages
            // that might be local instead in the workshop folder.
            return 2342568; // random const value: collisions are fine as we want to call Equals()
        }
        
        public static implicit operator DependencyEntryKey(ContentPackage package) => new(package);
        public static implicit operator DependencyEntryKey((string packageName, ulong steamWorkshopId) tuple1) => 
            new (tuple1.packageName, tuple1.steamWorkshopId);
        public static implicit operator DependencyEntryKey((string packageName, ulong steamWorkshopId, string folderPath) tuple1) => 
            new (tuple1.packageName, tuple1.folderPath, tuple1.steamWorkshopId);
    }
    
    
}
