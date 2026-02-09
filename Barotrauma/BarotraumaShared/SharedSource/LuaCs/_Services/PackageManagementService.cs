using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.Toolkit.Diagnostics;

namespace Barotrauma.LuaCs;

public sealed class PackageManagementService : IPackageManagementService
{
    // svc
    private ILoggerService _logger;
    private IModConfigService _modConfigService;
    private IConfigService _configService;
    private ILuaScriptManagementService _luaScriptManagementService;
    private IPluginManagementService _pluginManagementService;
    private IPackageManagementServiceConfig _runConfig;
    // state
    private readonly ConcurrentDictionary<ContentPackage, IModConfigInfo> _loadedPackages = new();
    private readonly ConcurrentDictionary<ContentPackage, IModConfigInfo> _runningPackages = new();
    // control
    /// <summary>
    /// Service Disposal Lock.
    /// </summary>
    private readonly AsyncReaderWriterLock _operationsLock = new();
    /// <summary>
    /// Execution of packages lock.
    /// <br/> Read: Package loading/unloading (Multi-operation mode).
    /// <br/> Write: Package execution (exclusive mode).
    /// </summary>
    private readonly AsyncReaderWriterLock _executionLock = new();
    
    public PackageManagementService(ILoggerService logger, 
        IModConfigService modConfigService, 
        ILuaScriptManagementService luaScriptManagementService, 
        IPluginManagementService pluginManagementService, 
        IConfigService configService, IPackageManagementServiceConfig runConfig)
    {
        _logger = logger;
        _modConfigService = modConfigService;
        _luaScriptManagementService = luaScriptManagementService;
        _pluginManagementService = pluginManagementService;
        _configService = configService;
        _runConfig = runConfig;
    }
    
    public void Dispose()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
            return;
        
        _logger.LogMessage($"{nameof(PackageManagementService)} is disposing.");
        _luaScriptManagementService.Dispose();
        _pluginManagementService.Dispose();
        _modConfigService.Dispose();
        _logger.Dispose();

        _logger = null;
        _luaScriptManagementService = null;
        _pluginManagementService = null;
        _modConfigService = null;
        _loadedPackages.Clear();
        _runningPackages.Clear();
    }

    private int _isDisposed = 0;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref  _isDisposed);
        set => ModUtils.Threading.SetBool(ref  _isDisposed, value);
    }
    
    public FluentResults.Result Reset()
    {
        using var lck  = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (IsDisposed)
            return FluentResults.Result.Fail($"{nameof(PackageManagementService)}failed to reset. Has already been disposed.");

        try
        {
            var operationResult = new FluentResults.Result();
            operationResult.WithReasons(_configService.Reset().Reasons);
            operationResult.WithReasons(_luaScriptManagementService.Reset().Reasons);
            operationResult.WithReasons(_pluginManagementService.Reset().Reasons);
            _runningPackages.Clear();
            _loadedPackages.Clear();
            return operationResult;
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(new ExceptionalError(e));
        }
    }

    public FluentResults.Result LoadPackageInfo(ContentPackage package)
    {
        Guard.IsNotNull(package, nameof(package));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var executeLock = _executionLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        
        IService.CheckDisposed(this);
        if (_loadedPackages.TryGetValue(package, out var result))
        {
            _logger.LogWarning($"{nameof(LoadPackageInfo)}: Tried to load already-loaded package {package.Name}.");
            return FluentResults.Result.Ok();
        }

        var pkgCfgInfo = _modConfigService.CreateConfigAsync(package).ConfigureAwait(false).GetAwaiter().GetResult();
        if (pkgCfgInfo.IsFailed)
        {
            _logger.LogResults(pkgCfgInfo.ToResult());
            return pkgCfgInfo.ToResult();
        }
        return UnsafeAddPackageInternal(package, pkgCfgInfo.Value);
    }

    public FluentResults.Result LoadPackagesInfo(ImmutableArray<ContentPackage> packages)
    {
        if (packages.IsDefaultOrEmpty)
            ThrowHelper.ThrowArgumentException($"{nameof(LoadPackagesInfo)}: packages list is empty.");
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var executeLock = _executionLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        
        IService.CheckDisposed(this);
        var result = new FluentResults.Result();
        var packages2 = packages.OrderBy(pkg => pkg.Name == "LuaCsForBarotrauma" ? 0 : 1) // always run lua cs first.
            .ThenBy(packages.IndexOf)
            .ToImmutableArray();
            
        var pkgConfigs = _modConfigService.CreateConfigsAsync([..packages2]).ConfigureAwait(false).GetAwaiter().GetResult();
        foreach (var pkgConfig in pkgConfigs)
        {
            result.WithReasons(pkgConfig.Config.Reasons);
            if (pkgConfig.Config.IsSuccess)
                result.WithReasons(UnsafeAddPackageInternal(pkgConfig.Source, pkgConfig.Config.Value).Reasons);
        }

        return result;
    }

    private FluentResults.Result UnsafeAddPackageInternal(ContentPackage package, IModConfigInfo config)
    {
        if (_loadedPackages.TryGetValue(package, out _))
        {
            _logger.LogWarning($"Tried to load already-loaded package {package.Name}.");
            return FluentResults.Result.Ok();
        }

        _loadedPackages[package] = config;
        try
        {
            var res = new FluentResults.Result();
            var tasks = ImmutableArray.CreateBuilder<Task<Task<FluentResults.Result>>>();

            if (!config.Configs.IsDefaultOrEmpty)
            {
                tasks.Add(Task.Factory.StartNew(async Task<FluentResults.Result> () =>
                    new FluentResults.Result()
                        .WithReasons((await _configService.LoadConfigsAsync(config.Configs)).Reasons)
                        .WithReasons((await _configService.LoadConfigsProfilesAsync(config.Configs)).Reasons)));
            }

            if (!config.LuaScripts.IsDefaultOrEmpty)
            {
                tasks.Add(Task.Factory.StartNew(async () =>
                    await _luaScriptManagementService.LoadScriptResourcesAsync(config.LuaScripts)));
            }

            if (tasks.Count == 0)
            {
                return FluentResults.Result.Ok();
            }
            
            var r = Task.WhenAll(tasks.ToArray()).ConfigureAwait(false).GetAwaiter().GetResult();

            foreach (var task in r)
                res.WithReasons(task.ConfigureAwait(false).GetAwaiter().GetResult().Reasons);
            return res;
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(new ExceptionalError(e));
        }
    }

    public FluentResults.Result ExecuteLoadedPackages(ImmutableArray<ContentPackage> executionOrder, bool executeCsAssemblies)
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var executeLock = _executionLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (executionOrder.IsDefaultOrEmpty)
        {
            return FluentResults.Result.Fail($"{nameof(ExecuteLoadedPackages)}: No packages in the execution order list.");
        }
        
        if (!_runningPackages.IsEmpty)
        {
            return FluentResults.Result.Fail(
                $"{nameof(ExecuteLoadedPackages)}: There are already packages running! List: {
                    _runningPackages.Aggregate(string.Empty, (acc, kvp) => "-" + kvp + "\n" + kvp.Key.Name)}");
        }

        if (_loadedPackages.IsEmpty)
        {
            return FluentResults.Result.Fail($"{nameof(ExecuteLoadedPackages)}: No packages loaded. Nothing to run!)");
        }

        var result = new FluentResults.Result();
        
        // get loading order. Note: packages not in the execution order list will load first.
        var loadingOrderedPackages = _loadedPackages
            .OrderBy(pkg => pkg.Key.Name == "LuaCsForBarotrauma" ? 0 : 1) // always run lua cs first.
            .ThenBy(pkg => executionOrder.IndexOf(pkg.Key))
            .ToImmutableArray();
        var loadOrderByPackage = loadingOrderedPackages.Select(p => p.Key).ToImmutableArray();
        var toLoadPackagesIndents = loadingOrderedPackages
            .SelectMany(p => p.Key.AltNames.Union(new []{ p.Key.Name }).ToIdentifiers())
            .ToImmutableHashSet();


        // NOTE: Config/Settings are instanced in LoadPackages()

        if (executeCsAssemblies)
        {
            var plugins = SelectCompatible(loadingOrderedPackages
                .SelectMany(pkg => pkg.Value.Assemblies)
                .ToImmutableArray(), toLoadPackagesIndents, loadOrderByPackage);

            if (!plugins.IsDefaultOrEmpty)
            {
                result.WithReasons(_pluginManagementService.LoadAssemblyResources(plugins).Reasons);
                result.WithReasons(_pluginManagementService.ActivatePluginInstances(
                    plugins.Select(p => p.OwnerPackage).ToImmutableArray(), false).Reasons);
            }
        }

        //lua scripts
        var luaScripts = SelectCompatible(loadingOrderedPackages
            .SelectMany(pkg => pkg.Value.LuaScripts)
            .ToImmutableArray(), toLoadPackagesIndents, loadOrderByPackage);
            
        if (!luaScripts.IsDefaultOrEmpty)
        {
            result.WithReasons(_luaScriptManagementService.ExecuteLoadedScripts(luaScripts);
        }

        foreach (var package in loadingOrderedPackages)
        {
            _runningPackages[package.Key] = package.Value;
        }

        if (result.IsFailed)
        {
            _logger.LogResults(result);
        }
        return result;
    }
    
    private static ImmutableArray<T> SelectCompatible<T>(ImmutableArray<T> resources, 
        ImmutableHashSet<Identifier> enabledPackagesIdents, 
        ImmutableArray<ContentPackage> loadingOrder)
        where T : IBaseResourceInfo
    {
        return resources
            .Where(r => r.SupportedPlatforms.HasFlag(ModUtils.Environment.CurrentPlatform))
            .Where(r => r.SupportedTargets.HasFlag(ModUtils.Environment.CurrentTarget))
            .Where(r => !r.Optional || (
            (r.RequiredPackages.IsDefaultOrEmpty || enabledPackagesIdents.Intersect(r.RequiredPackages).Any()) 
            && (r.IncompatiblePackages.IsDefaultOrEmpty || enabledPackagesIdents.Intersect(r.IncompatiblePackages).None())))
            .OrderBy(r => r.Optional ? 1 : 0)   // optional content last
            .ThenBy(r => loadingOrder.IndexOf(r.OwnerPackage))
            .ThenBy(r => r.LoadPriority)
            .ToImmutableArray();
    }
    
    
    public FluentResults.Result SyncLoadedPackagesList(ImmutableArray<ContentPackage> packages)
    {
        if (packages.IsDefaultOrEmpty)
            ThrowHelper.ThrowArgumentNullException(nameof(packages));
        if (!_runningPackages.IsEmpty)
            ThrowHelper.ThrowInvalidOperationException($"{nameof(SyncLoadedPackagesList)}: There are packages running!");
        
        var toRemove = _loadedPackages.Keys.Except(packages).ToImmutableArray();
        var toAdd = packages.Except(_loadedPackages.Keys)
            .OrderBy(pack => packages.IndexOf(pack)).ToImmutableArray();

        var result = new FluentResults.Result();

        if (!toRemove.IsDefaultOrEmpty)
        {
            result.WithReasons(UnloadPackages(toRemove).Reasons);
        }
        
        if (!toAdd.IsDefaultOrEmpty)
        {
            result.WithReasons(LoadPackagesInfo(toAdd).Reasons);
        }
        
        return result;
    }

    public FluentResults.Result StopRunningPackages()
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var executeLock = _executionLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        if (_loadedPackages.IsEmpty || _runningPackages.IsEmpty)
        {
            _logger.LogWarning($"{nameof(StopRunningPackages)}: No packages are currently executing.");
            return FluentResults.Result.Ok();
        }
        
        var res = new FluentResults.Result();
        res.WithReasons(_luaScriptManagementService.UnloadActiveScripts().Reasons);
        res.WithReasons(_pluginManagementService.UnloadManagedAssemblies().Reasons);
        _runningPackages.Clear();
        return res;
    }
    
    public FluentResults.Result UnloadPackage(ContentPackage package)
    {
        Guard.IsNotNull(package, nameof(package));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var executeLock = _executionLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        if (!_loadedPackages.ContainsKey(package))
            return FluentResults.Result.Fail($"{nameof(UnloadPackage)}: The package is not loaded.");
        if (!_runningPackages.IsEmpty)
            return FluentResults.Result.Fail($"{nameof(UnloadPackage)}: Packages are currently executing.");
        var result = new  FluentResults.Result();
        result.WithReasons(_luaScriptManagementService.DisposePackageResources(package).Reasons);
        result.WithReasons(_configService.DisposePackageData(package).Reasons);
        _loadedPackages.TryRemove(package, out _);
        return result;
    }
    
    public FluentResults.Result UnloadPackages(ImmutableArray<ContentPackage> packages)
    {
        if (packages.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(UnloadPackages)}: Package list is empty.");
        
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var executeLock = _executionLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        var result =  new FluentResults.Result();
        foreach (var package in packages)
            result.WithReasons(UnloadPackage(package).Reasons);
        return result;
    }

    public FluentResults.Result UnloadAllPackages()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var executeLock = _executionLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        if (_loadedPackages.IsEmpty)
            return FluentResults.Result.Ok();
        if (!_runningPackages.IsEmpty)
            return FluentResults.Result.Fail($"{nameof(UnloadAllPackages)}: Packages are currently executing.");
        var result = new FluentResults.Result();
        result.WithReasons(_luaScriptManagementService.DisposeAllPackageResources().Reasons);
        result.WithReasons(_configService.DisposeAllPackageData().Reasons);
        _loadedPackages.Clear();
        return result;
    }

    public ImmutableArray<ContentPackage> GetAllLoadedPackages()
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        return [.._loadedPackages.Keys];
    }

    public bool IsPackageRunning(ContentPackage package)
    {
        Guard.IsNotNull(package, nameof(package));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        return _runningPackages.ContainsKey(package);
    }

    public bool IsAnyPackageLoaded()
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        return !_loadedPackages.IsEmpty;
    }

    public bool IsAnyPackageRunning()
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        return !_runningPackages.IsEmpty;
    }

    public ImmutableArray<ContentPackage> GetLoadedAssemblyPackages()
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        if (_loadedPackages.IsEmpty)
            return ImmutableArray<ContentPackage>.Empty;
        return [.._loadedPackages.Values
                .Where(cfg => !cfg.Assemblies.IsDefaultOrEmpty)
                .Select(cfg => cfg.Package)];
    }
}
