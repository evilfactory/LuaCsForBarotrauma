using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
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

    private readonly ReaderWriterLockSlim _contentPackagesQueueLock = new();
    
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
        _contentPackagesQueueLock.EnterWriteLock();
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
            _contentPackagesQueueLock.ExitWriteLock();
        }
    }

    public FluentResults.Result ProcessQueuedPackages(bool rescanPackages = false, bool loadParallel = true, bool reportFailOnDuplicates = false)
    {
        _contentPackagesQueueLock.EnterReadLock();
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
             * Return array: (Normal, MissingDeps)
             */
            FluentResults.Result<(ImmutableArray<T>, ImmutableArray<T>)> GetLoadablePackages<T>(ImmutableArray<T> resources, bool errorForPacksMissingDeps = false)
                where T : class, IPackageDependenciesInfo, IPackageInfo, IResourceInfo, IResourceCultureInfo
            {
                throw new NotImplementedException();
                
                // filter optional resources (process later)
                // add back in optional packages that are required by other required packages
                // filter and log required packages that are missing dependencies
                // re-include optionals where dependencies are available
                // return both lists (A normal, B missingDeps).
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
            _contentPackagesQueueLock.ExitReadLock();
        }
    }

    public FluentResults.Result UnloadPackages(bool errorOnFailures = true)
    {
        throw new NotImplementedException();
    }

    public bool IsPackageLoaded(ContentPackage package)
    {
        throw new NotImplementedException();
    }

    public bool CheckDependencyLoaded(IPackageDependencyInfo info)
    {
        throw new NotImplementedException();
    }

    public bool CheckDependenciesLoaded(IEnumerable<IPackageDependencyInfo> infos, out IReadOnlyList<IPackageDependencyInfo> missingPackages)
    {
        throw new NotImplementedException();
    }

    public bool CheckEnvironmentSupported(IPlatformInfo platform)
    {
        throw new NotImplementedException();
    }
}
