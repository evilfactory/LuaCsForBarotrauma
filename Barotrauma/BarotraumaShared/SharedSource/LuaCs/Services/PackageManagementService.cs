using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public sealed class PackageManagementService : IPackageManagementService
{
    // svc
    private ILoggerService _logger;
    private IModConfigService _modConfigService;
    private ILuaScriptManagementService _luaScriptManagementService;
    private IPluginManagementService _pluginManagementService;
    // state
    private readonly ConcurrentDictionary<ContentPackage, IModConfigInfo> _loadedPackages = new();
    private readonly ConcurrentDictionary<ContentPackage, IModConfigInfo> _runningPackages = new();
    // control
    private readonly AsyncReaderWriterLock _operationsLock = new();
    
    public PackageManagementService(ILoggerService logger, IModConfigService modConfigService, ILuaScriptManagementService luaScriptManagementService, IPluginManagementService pluginManagementService)
    {
        _logger = logger;
        _modConfigService = modConfigService;
        _luaScriptManagementService = luaScriptManagementService;
        _pluginManagementService = pluginManagementService;
    }
    
    public void Dispose()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
            return;
        
        _logger.LogMessage($"{nameof(PackageManagementService)} is disposing");
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

        var operationResult = new FluentResults.Result();
        CombineResultErrors(operationResult, UnsafeStopRunningPackagesInternal());
        CombineResultErrors(operationResult, UnsafeUnloadAllPackagesInternal());
        return operationResult;
        
        void CombineResultErrors(FluentResults.Result result,
            ImmutableArray<(ContentPackage Package, FluentResults.Result OperationResult)> packRes)
        {
            if (packRes.IsDefaultOrEmpty) 
                return;
            
            foreach (var r in packRes)
            {
                if (r.OperationResult.IsSuccess)
                    continue;
                _logger.LogResults(r.OperationResult);
                result.WithErrors(r.OperationResult.Errors);
            }
        }
    }

    public FluentResults.Result LoadPackageInfo(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }

    public ImmutableArray<(ContentPackage Package, FluentResults.Result LoadSuccessResult)> LoadPackagesInfo(IReadOnlyCollection<ContentPackage> packages)
    {
        throw new System.NotImplementedException();
    }

    private FluentResults.Result UnsafeLoadPackageInfoInternal(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }

    public ImmutableArray<(ContentPackage Package, FluentResults.Result ExecutionResult)> ExecuteLoadedPackages()
    {
        throw new System.NotImplementedException();
    }

    private ImmutableArray<(ContentPackage Package, FluentResults.Result StopExectionResult)> UnsafeStopRunningPackagesInternal()
    {
        throw new System.NotImplementedException();    
    }
    
    public ImmutableArray<(ContentPackage Package, FluentResults.Result StopExecutionResult)> StopRunningPackages()
    {
        throw new System.NotImplementedException();
    }

    private FluentResults.Result UnsafeUnloadPackageInternal(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }

    private ImmutableArray<(ContentPackage Package, FluentResults.Result UnloadSuccessResult)> UnsafeUnloadAllPackagesInternal()
    {
        throw new System.NotImplementedException();
    }
    
    public FluentResults.Result UnloadPackage(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }
    
    public ImmutableArray<(ContentPackage Package, FluentResults.Result UnloadSuccessResult)> UnloadPackages(IReadOnlyCollection<ContentPackage> packages)
    {
        throw new System.NotImplementedException();
    }

    public ImmutableArray<(ContentPackage Package, FluentResults.Result UnloadSuccessResult)> UnloadAllPackages()
    {
        throw new System.NotImplementedException();
    }
}
