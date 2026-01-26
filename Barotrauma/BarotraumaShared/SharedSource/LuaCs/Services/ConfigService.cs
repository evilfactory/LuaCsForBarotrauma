using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Processing;
using FluentResults;
using Microsoft.Toolkit.Diagnostics;

namespace Barotrauma.LuaCs.Services;

public sealed partial class ConfigService : IConfigService
{
    #region Disposal_Locks_Reset

    private readonly AsyncReaderWriterLock _operationLock = new ();
    private readonly AsyncReaderWriterLock _settingsByPackageLock = new ();
    private int _isDisposed = 0;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }
    
    public void Dispose()
    {
        using var lck = _operationLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var settingsLck = _settingsByPackageLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
        {
            return;
        }
        
        _logger.LogDebug($"{nameof(ConfigService)}: Disposing.");
        
        _configInfoParserService.Dispose();
        _configProfileInfoParserService.Dispose();

        if (!_settingsInstances.IsEmpty)
        {
            foreach (var instance in _settingsInstances)
            {
                try
                {
                    if (instance.Value is null)
                    {
                        continue;
                    }

                    _eventService.PublishEvent<IEventSettingInstanceLifetime>(sub =>
                        // ReSharper disable once AccessToDisposedClosure
                        sub.OnSettingInstanceDisposed(instance.Value));
                    instance.Value.Dispose();
                }
                catch 
                {
                    // ignored
                    continue;
                }
            }
        }
        
        _settingsInstances.Clear();
        _instanceFactory.Clear();
        _settingsInstancesByPackage.Clear();
        
        _storageService = null;
        _logger = null;
        _eventService = null;
        _configInfoParserService = null;
        _configProfileInfoParserService = null;
    }
    
    public FluentResults.Result Reset()
    {
        using var lck = _operationLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        var result = new FluentResults.Result();
        
        if (!_settingsInstances.IsEmpty)
        {
            foreach (var instance in _settingsInstances)
            {
                try
                {
                    if (instance.Value is null)
                    {
                        continue;
                    }

                    _eventService.PublishEvent<IEventSettingInstanceLifetime>(sub =>
                        // ReSharper disable once AccessToDisposedClosure
                        sub.OnSettingInstanceDisposed(instance.Value));
                    instance.Value.Dispose();
                }
                catch (Exception e)
                {
                    result.WithError(new ExceptionalError(e));
                }
            }
        }
        
        _settingsInstances.Clear();
        _instanceFactory.Clear();
        _settingsInstancesByPackage.Clear();

        return result;
    }

    #endregion

    
    private readonly ConcurrentDictionary<(ContentPackage OwnerPackage, string InternalName), ISettingBase> 
        _settingsInstances = new();
    private readonly ConcurrentDictionary<string, Func<IConfigInfo, ISettingBase>>
        _instanceFactory = new();
    private readonly ConcurrentDictionary<ContentPackage, ConcurrentBag<ISettingBase>>
        _settingsInstancesByPackage = new();
    
    private IStorageService _storageService;
    private ILoggerService _logger;
    private IEventService _eventService;
    private IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigInfo> _configInfoParserService;
    private IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigProfileInfo> _configProfileInfoParserService;

    public ConfigService(ILoggerService logger, 
        IStorageService storageService, 
        IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigInfo> configInfoParserService, 
        IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigProfileInfo> configProfileInfoParserService, IEventService eventService)
    {
        _logger = logger;
        _storageService = storageService;
        _configInfoParserService = configInfoParserService;
        _configProfileInfoParserService = configProfileInfoParserService;
        _eventService = eventService;
    }


    public void RegisterSettingTypeInitializer<T>(string typeIdentifier, Func<IConfigInfo, T> settingFactory) where T : class, ISettingBase
    {
        Guard.IsNotNullOrWhiteSpace(typeIdentifier, nameof(typeIdentifier));
        Guard.IsNotNull(settingFactory, nameof(settingFactory));
        using var lck = _operationLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (_instanceFactory.ContainsKey(typeIdentifier))
        {
            ThrowHelper.ThrowArgumentException($"{nameof(RegisterSettingTypeInitializer)}: The type identifier {typeIdentifier} is already registered.");
        }
        
        _instanceFactory[typeIdentifier] = settingFactory;
    }

    public async Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources)
    {
        using var lck = _operationLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        if (configResources.IsDefaultOrEmpty)
        {
            return FluentResults.Result.Ok();
        }

        var taskBuilder = ImmutableArray.CreateBuilder<Task<ImmutableArray<IConfigInfo>>>();
        var toProcessErrors = new ConcurrentStack<IError>();
        
        foreach (var resource in configResources)
        {
            taskBuilder.Add(await Task.Factory.StartNew<Task<ImmutableArray<IConfigInfo>>>(async Task<ImmutableArray<IConfigInfo>> () =>
            {
                var r = await _configInfoParserService.TryParseResourcesAsync(resource);
                if (r.IsFailed)
                {
                    toProcessErrors.PushRange(r.Errors.ToArray());
                    return ImmutableArray<IConfigInfo>.Empty;
                }
                return r.Value;
            }));
        }

        var taskResults = await Task.WhenAll(taskBuilder.ToImmutable());

        if (toProcessErrors.Count > 0)
        {
            return FluentResults.Result.Fail($"{nameof(LoadConfigsAsync)}: Errors while loading configuration info: ").WithErrors(toProcessErrors.ToArray());
        }
        
        var toProcessDocs = taskResults
            .Where(tr => !tr.IsDefaultOrEmpty)
            .SelectMany(tr => tr)
            .ToImmutableArray();

        var instanceQueue = new Queue<(IConfigInfo configInfo, Func<IConfigInfo, ISettingBase> factory)>();
        
        foreach (var info in toProcessDocs)
        {
            if (!_instanceFactory.TryGetValue(info.DataType, out var factory))
            {
                return FluentResults.Result.Fail($"{nameof(LoadConfigsAsync)}: Could not retrieve the instance factory for the data type of '{info.DataType}'!");
            }
            if (_settingsInstances.ContainsKey((info.OwnerPackage, info.InternalName)))
            {
                // duplicate for some reason (ie. double loading). This should never happen.
                ThrowHelper.ThrowInvalidOperationException($"{nameof(LoadConfigsAsync)}: A setting for the [ContentPackage].[InternalName] of '[{info.OwnerPackage.Name}].[{info.InternalName}]' already exists!");
            }
            
            instanceQueue.Enqueue((info, factory));
        }

        var toProcessInstanceQueue = new Queue<(IConfigInfo info, ISettingBase instance)>();

        while (instanceQueue.TryDequeue(out var instanceFactoryInfo))
        {
            try
            {
                toProcessInstanceQueue.Enqueue((instanceFactoryInfo.configInfo, instanceFactoryInfo.factory(instanceFactoryInfo.configInfo)));
            }
            catch (Exception e)
            {
                FluentResults.Result.Fail(
                    $"{nameof(LoadConfigsAsync)}: Error while instancing setting for '{instanceFactoryInfo.configInfo.OwnerPackage}.{instanceFactoryInfo.configInfo.InternalName}': {e.Message}!");
            }
        }

        using var settingsLck = await _settingsByPackageLock.AcquireWriterLock(); // block to protect new bag instance creation
        var result = new FluentResults.Result();
        
        while (toProcessInstanceQueue.TryDequeue(out var newInstanceData))
        {
            _settingsInstances[(newInstanceData.info.OwnerPackage, newInstanceData.info.InternalName)] =  newInstanceData.instance;
            if (!_settingsInstancesByPackage.TryGetValue(newInstanceData.info.OwnerPackage, out _))
            {
                _settingsInstancesByPackage[newInstanceData.info.OwnerPackage] = new ConcurrentBag<ISettingBase>();
            }
            _settingsInstancesByPackage[newInstanceData.info.OwnerPackage].Add(newInstanceData.instance);
            result.WithReasons(_eventService.PublishEvent<IEventSettingInstanceLifetime>(sub =>
                sub.OnSettingInstanceCreated(newInstanceData.instance)).Reasons);
        }

        return result;
    }

    public async Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigResourceInfo> configProfileResources)
    {
#if DEBUG
        // TODO: Implement profiles.
        return FluentResults.Result.Ok();
#endif
        throw new NotImplementedException();
    }

    public FluentResults.Result DisposePackageData(ContentPackage package)
    {
        Guard.IsNotNull(package, nameof(package));
        using var lck = _operationLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        ConcurrentBag<ISettingBase> toDispose;
        using (var settingsLck = _settingsByPackageLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult())
        {
            if (!_settingsInstancesByPackage.TryRemove(package, out toDispose) || toDispose is null)
            {
                return FluentResults.Result.Ok();
            }
        }

        var result = new FluentResults.Result();

        foreach (var setting in toDispose)
        {
            result.WithReasons(_eventService.PublishEvent<IEventSettingInstanceLifetime>(sub => sub.OnSettingInstanceDisposed(setting)).Reasons);
            try
            {
                setting.Dispose();
            }
            catch (Exception e)
            {
                result.WithError(new ExceptionalError(e));
            }
        }
        
        return result;
    }

    public FluentResults.Result DisposeAllPackageData()
    {
        return this.Reset();
    }

    public bool TryGetConfig<T>(ContentPackage package, string internalName, out T instance) where T : ISettingBase
    {
        Guard.IsNotNull(package, nameof(package));
        Guard.IsNotNullOrWhiteSpace(internalName, nameof(internalName));
        using var lck = _operationLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        using var settingsLck =
            _settingsByPackageLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        instance = default;
        
        if(!_settingsInstances.TryGetValue((package, internalName), out var inst))
        {
            return false;
        }

        if (inst is not T instanceT)
        {
            return false;
        }
        
        instance = instanceT;
        return true;
    }
}
