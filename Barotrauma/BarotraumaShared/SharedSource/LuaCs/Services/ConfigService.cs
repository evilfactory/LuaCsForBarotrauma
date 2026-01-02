using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Processing;
using Barotrauma.Networking;
using Dynamitey.DynamicObjects;
using FluentResults;
using Microsoft.Xna.Framework;
using OneOf;
using Path = Barotrauma.IO.Path;

namespace Barotrauma.LuaCs.Services;

public partial class ConfigService : IConfigService
{
    //--- Internals
    public ConfigService(IParserServiceAsync<IConfigProfileResourceInfo, IReadOnlyList<IConfigProfileInfo>> configProfileResourceParser, 
        IParserServiceAsync<IConfigResourceInfo, IReadOnlyList<IConfigInfo>> configResourceParser, IEventService eventService, System.Lazy<IStorageService> storageService)
    {
        _configProfileResourceParser = configProfileResourceParser;
        _configResourceParser = configResourceParser;
        _eventService = eventService;
        _storageService = storageService;
        this._base = this;
    }
    
    // data, states
    private readonly IService _base;
    private int _isDisposed = 0;
    private readonly ConcurrentDictionary<Type, Func<IConfigInfo, FluentResults.Result<IConfigBase>>> _configTypeInitializers = new();
    private readonly ConcurrentDictionary<(ContentPackage Package, string ConfigName), IConfigBase> _configs = new();
    private readonly ConcurrentDictionary<ContentPackage, ConcurrentBag<(ContentPackage Package, string ConfigName)>> _packageConfigReverseLookup = new();
    private readonly ConcurrentDictionary<(ContentPackage Package, string ProfileName), ImmutableArray<(string ConfigName, OneOf.OneOf<string, XElement> Value)>> _configProfiles = new();
    private readonly ConcurrentDictionary<ContentPackage, ConcurrentBag<(ContentPackage Package, string ProfileName)>> _packageProfilesReverseLookup = new();
    private readonly ConcurrentDictionary<string, ContentPackage> _packageNameMap= new();
    
    private readonly AsyncReaderWriterLock _disposeOpsLock = new();
    
    // extern services
    private readonly IParserServiceAsync<IConfigResourceInfo, IReadOnlyList<IConfigInfo>> _configResourceParser;
    private readonly IParserServiceAsync<IConfigProfileResourceInfo, IReadOnlyList<IConfigProfileInfo>> _configProfileResourceParser;
    private readonly IEventService _eventService;
    private readonly System.Lazy<IStorageService> _storageService;
    
    //--- GC
    public bool IsDisposed => ModUtils.Threading.GetBool(ref _isDisposed);

    public void Dispose()
    {
        // stop all ops
        using var lck = _disposeOpsLock.AcquireWriterLock().GetAwaiter().GetResult();
        // set flag
        ModUtils.Threading.SetBool(ref _isDisposed, true);
        
        _configTypeInitializers.Clear();
        if (!_configs.IsEmpty)
        {
            foreach (var config in _configs)
            {
                if (config.Value is IDisposable disposable)
                    disposable.Dispose();
                config.Value.OnValueChanged -= this.SaveConfigEvent;
            }
            _configs.Clear();
        }
        
        _configProfiles.Clear();
        _packageConfigReverseLookup.Clear();
        _packageNameMap.Clear();
        _packageProfilesReverseLookup.Clear();
        
        GC.SuppressFinalize(this);
    }
    
    public FluentResults.Result Reset()
    {
        using var lck = _disposeOpsLock.AcquireWriterLock().GetAwaiter().GetResult();
        _base.CheckDisposed();
     
        _configTypeInitializers.Clear();
        _configs.Clear();
        _configProfiles.Clear();
        _packageConfigReverseLookup.Clear();
        _packageNameMap.Clear();
        _packageProfilesReverseLookup.Clear();

        return FluentResults.Result.Ok();
    }

    //--- API contracts
    // Notes:
    // -- Lua Interface uses strong types due to lua limitations. May be required to move API to an adapter class 
    // to allow testing abstraction.
    // -- Lua interface should not propagate errors.
    #region LuaInterface

    private bool TryGetConfigValue<T>(string packageName, string configName, out T value) where T : IEquatable<T>
    {
        value = default;
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        if (ModUtils.Threading.GetBool(ref _isDisposed))
            return false;
        if (!_packageNameMap.TryGetValue(packageName, out var package))
            return false;
        if (!_configs.TryGetValue((package, configName), out var config))
            return false;
        if (config is not IConfigEntry<T> entry)
            return false;
        value = entry.Value;
        return true;
    }
    
    public bool TryGetConfigBool(string packageName, string configName, out bool value)
    {
        return TryGetConfigValue(packageName, configName, out value);
    }

    public bool TryGetConfigInt(string packageName, string configName, out int value)
    {
        return TryGetConfigValue(packageName, configName, out value);
    }

    public bool TryGetConfigFloat(string packageName, string configName, out float value)
    {
        return TryGetConfigValue(packageName, configName, out value);
    }

    public bool TryGetConfigNumber(string packageName, string configName, out double value)
    {
        return TryGetConfigValue(packageName, configName, out value);
    }

    public bool TryGetConfigString(string packageName, string configName, out string value)
    {
        return TryGetConfigValue(packageName, configName, out value);
    }

    public bool TryGetConfigVector2(string packageName, string configName, out Vector2 value)
    {
        return TryGetConfigValue(packageName, configName, out value);
    }

    public bool TryGetConfigVector3(string packageName, string configName, out Vector3 value)
    {
        return TryGetConfigValue(packageName, configName, out value);
    }

    public bool TryGetConfigColor(string packageName, string configName, out Color value)
    {
        return TryGetConfigValue(packageName, configName, out value);
    }

    public bool TryGetConfigList(string packageName, string configName, out IReadOnlyList<string> value)
    {
        value = null;
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        if (ModUtils.Threading.GetBool(ref _isDisposed))
            return false;
        if (!_packageNameMap.TryGetValue(packageName, out var package))
            return false;
        if (!_configs.TryGetValue((package, configName), out var config))
            return false;
        if (config is not IConfigList<string> entry)
            return false;
        value = entry.Options;
        return value is not null && value.Count > 0;
    }

    private void SetConfigValue<T>(string packageName, string configName, T value) where T : IEquatable<T>
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        if (ModUtils.Threading.GetBool(ref _isDisposed))
            return;
        if (!_packageNameMap.TryGetValue(packageName, out var package))
            return;
        if (!_configs.TryGetValue((package, configName), out var config))
            return;
        if (config is not IConfigEntry<T> entry)
            return;
        entry.TrySetValue(value);
    }

    public void SetConfigBool(string packageName, string configName, bool value)
    {
        SetConfigValue(packageName, configName, value);
    }

    public void SetConfigInt(string packageName, string configName, int value)
    {
        SetConfigValue(packageName, configName, value);
    }

    public void SetConfigFloat(string packageName, string configName, float value)
    {
        SetConfigValue(packageName, configName, value);
    }

    public void SetConfigNumber(string packageName, string configName, double value)
    {
        SetConfigValue(packageName, configName, value);
    }

    public void SetConfigString(string packageName, string configName, string value)
    {
        SetConfigValue(packageName, configName, value);
    }

    public void SetConfigVector2(string packageName, string configName, Vector2 value)
    {
        SetConfigValue(packageName, configName, value);
    }

    public void SetConfigVector3(string packageName, string configName, Vector3 value)
    {
        SetConfigValue(packageName, configName, value);
    }

    public void SetConfigColor(string packageName, string configName, Color value)
    {
        SetConfigValue(packageName, configName, value);
    }

    public void SetConfigList(string packageName, string configName, string value)
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        if (ModUtils.Threading.GetBool(ref _isDisposed))
            return;
        if (!_packageNameMap.TryGetValue(packageName, out var package))
            return;
        if (!_configs.TryGetValue((package, configName), out var config))
            return;
        if (config is not IConfigList<string> entry)
            return;
        entry.TrySetValue(value);
    }

    public bool TryApplyProfileSettings(string packageName, string profileName)
    {
        
        if (ModUtils.Threading.GetBool(ref _isDisposed))
            return false;
        if (packageName.IsNullOrWhiteSpace() || profileName.IsNullOrWhiteSpace())
            return false;
        ContentPackage package = null;
        using (var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult())
        {
            if (!_packageNameMap.TryGetValue(packageName, out package) || package == null)
                return false;
        }
        // exit semaphore before invocation. Note: Race condition, may require copy implementation.
        return this.ApplyProfileSettings(package, profileName).IsSuccess;
    }

    #endregion

    public void RegisterTypeInitializer<TData, TConfig>(Func<IConfigInfo, FluentResults.Result<TConfig>> initializer, bool replaceIfExists = false) 
        where TData : IEquatable<TData> where TConfig : IConfigBase
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        _base.CheckDisposed();
        
        Type dataType = typeof(TData);
        if (_configTypeInitializers.ContainsKey(dataType) && !replaceIfExists)
            return;
        _configTypeInitializers[dataType] = (info =>
        {
            var res = initializer(info);
            if (res.IsFailed)
                return FluentResults.Result.Fail($"Failed to initialize config type {dataType.Name}").WithErrors(res.Errors);
            return res.Value;
        });
    }

    private void AddConfigInstance((ContentPackage Package, string ConfigName) key, IConfigBase instance)
    {
        _configs[key] = instance;
        if (!_packageNameMap.ContainsKey(key.Package.Name))
            _packageNameMap[key.Package.Name] = key.Package;
        if (!_packageConfigReverseLookup.TryGetValue(key.Package, out var list))
        {
            list = new ConcurrentBag<(ContentPackage Package, string ConfigName)>();
            _packageConfigReverseLookup[key.Package] = list;
        }
        list.Add(key);
        // save hook
        instance.OnValueChanged += this.SaveConfigEvent;
        _eventService.PublishEvent<IEventConfigVarInstanced>(sub => sub.OnConfigCreated(instance));
    }

    private void AddProfileInstance((ContentPackage Package, string ProfileName) key, IConfigProfileInfo profile)
    {
        _configProfiles[key] = profile.ProfileValues.ToImmutableArray();
        if (!_packageNameMap.ContainsKey(key.Package.Name))
            _packageNameMap[key.Package.Name] = key.Package;
        if (!_packageProfilesReverseLookup.TryGetValue(key.Package, out var list))
        {
            list = new ConcurrentBag<(ContentPackage Package, string ProfileName)>();
            _packageProfilesReverseLookup[key.Package] = list;
        }
        list.Add(key);
    }
    
    public async Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources)
    {
        using var lck = await _disposeOpsLock.AcquireReaderLock();
        _base.CheckDisposed();
        
        if (configResources.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(LoadConfigsAsync)}: Array is empty.");
            
        var results = await _configResourceParser.TryParseResourcesAsync(configResources);
        var ret = new FluentResults.Result();
        
        foreach (var result in results)
        {
            if (result.Errors.Any())
                ret.Errors.AddRange(result.Errors);
            if (result.IsFailed || result.Value is not { Count: > 0 } res)
                continue;

            foreach (var configInfo in res)   
            {
                if (_configs.ContainsKey((configInfo.OwnerPackage, configInfo.InternalName)))
                {
                    ret.Errors.Add(new Error($"{nameof(LoadConfigsAsync)}: Config already exists for the compound key {configInfo.OwnerPackage.Name} | {configInfo.InternalName}"));
                    continue;
                }
                
                if (!_configTypeInitializers.TryGetValue(configInfo.DataType, out var initializer))
                {
                    ret.Errors.Add(new Error($"{nameof(LoadConfigsAsync)} No type initializer for {configInfo.DataType}"));
                    continue;
                }
                
                

                var cfg = initializer(configInfo);
                if (cfg.Errors.Any())
                    ret.Errors.AddRange(cfg.Errors);
                if (cfg.IsFailed || cfg.Value is not {} val)
                    continue;
                
                AddConfigInstance((configInfo.OwnerPackage, configInfo.InternalName), val);
            }
        }

        return ret;
    }

    public async Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigProfileResourceInfo> configProfileResources)
    {
        using var lck = await _disposeOpsLock.AcquireReaderLock();
        _base.CheckDisposed();
        
        if (configProfileResources.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(LoadConfigsProfilesAsync)}: Array is empty.");
            
        var results = await _configProfileResourceParser.TryParseResourcesAsync(configProfileResources);
        var ret = new FluentResults.Result();
        
        foreach (var result in results)
        {
            if (result.Errors.Any())
                ret.Errors.AddRange(result.Errors);
            if (result.IsFailed || result.Value is not { Count: > 0 } res)
                continue;

            foreach (var profileInfo in res)   
            {
                if (_configProfiles.ContainsKey((profileInfo.OwnerPackage, profileInfo.InternalName)))
                {
                    ret.Errors.Add(new Error($"{nameof(LoadConfigsProfilesAsync)}: Config already exists for the compound key {profileInfo.OwnerPackage.Name} | {profileInfo.InternalName}"));
                    continue;
                }
                
                AddProfileInstance((profileInfo.OwnerPackage, profileInfo.InternalName), profileInfo);
            }
        }

        return ret;
    }

    public FluentResults.Result<TConfig> AddConfig<TConfig>(IConfigInfo configInfo) where TConfig : IConfigBase
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        _base.CheckDisposed();
        
        if (configInfo is null)
            return FluentResults.Result.Fail($"{nameof(AddConfig)}: Config is null.");
        
        if (!_configTypeInitializers.TryGetValue(configInfo.DataType, out var initializer))
            return FluentResults.Result.Fail($"{nameof(AddConfig)}: No type initializer for {configInfo.DataType}");

        var errList = new List<IError>();
        
        try
        {
            var cfg = initializer(configInfo);
            if (cfg.Errors.Any())
                errList.AddRange(cfg.Errors);
            if (cfg.IsFailed || cfg.Value is null)
                return FluentResults.Result.Fail($"Failed to initialize {configInfo.DataType}").WithErrors(errList);
            AddConfigInstance((configInfo.OwnerPackage, configInfo.InternalName), cfg.Value);
            return (TConfig)cfg.Value;
        }
        catch(Exception ex)
        {
            return FluentResults.Result.Fail($"Failed to initialize {configInfo.DataType}").WithError(new ExceptionalError(ex));
        }
    }

    public FluentResults.Result ApplyProfileSettings(ContentPackage package, string profileName)
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        _base.CheckDisposed();
        
        if (package == null || string.IsNullOrEmpty(profileName))
            return FluentResults.Result.Fail($"{nameof(ApplyProfileSettings)}: ContentPackage and/or name were null or empty.");

        if (!_configProfiles.TryGetValue((package, profileName), out var list))
            return FluentResults.Result.Fail($"No profiles found for package {package.Name} with name {profileName}");
        
        if (list.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(ApplyProfileSettings)}: No stored values for profile {profileName}.");

        var errList = new List<IError>();
        
        foreach (var profileVal in list)
        {
            if (!_configs.TryGetValue((package, profileVal.ConfigName), out var val))
                continue;
            
            if (!val.TrySetValue(profileVal.Value))
                errList.Add(new Error($"Failed to apply value from profile named {profileName} to {val.InternalName}"));
            // continue
        }
        
        return FluentResults.Result.Ok().WithErrors(errList);
    }

    public FluentResults.Result DisposePackageData(ContentPackage package)
    {
        // stop regular ops during deletion ops
        using var lck = _disposeOpsLock.AcquireWriterLock().GetAwaiter().GetResult();
        _base.CheckDisposed();
        
        if (package is null)
            return FluentResults.Result.Fail($"{nameof(DisposePackageData)}: Package was null.");

        var errList = new List<IError>();
        
        if (_packageConfigReverseLookup.Remove(package, out var cfgKeys))
        {
            if (cfgKeys.Any())
            {
                foreach (var key in cfgKeys)
                {
                    try
                    {
                        _configs.Remove(key, out var cfg);
                        cfg?.Dispose();
                    }
                    catch (Exception e)
                    {
                        errList.Add(new ExceptionalError(e));
                    }
                }
            }
        }

        if (_packageProfilesReverseLookup.Remove(package, out var profileKeys))
        {
            if (profileKeys.Any())
            {
                foreach (var key in profileKeys)
                {
                    _configProfiles.Remove(key, out _);
                }
            }
        }

        _packageNameMap.Remove(package.Name, out _);
        
        return FluentResults.Result.Ok().WithErrors(errList);
    }

    public Result<IReadOnlyDictionary<(ContentPackage Package, string ConfigName), IConfigBase>> GetConfigsForPackage(ContentPackage package)
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        _base.CheckDisposed();

        if (!_packageConfigReverseLookup.TryGetValue(package, out var keys) || keys.IsEmpty)
            return FluentResults.Result.Fail($"No configs found for package {package.Name}");

        return _configs.Where(kvp => keys.Contains(kvp.Key)).ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public Result<IReadOnlyDictionary<(ContentPackage Package, string ConfigName), ImmutableArray<(string ConfigName, OneOf<string, XElement> Value)>>> GetProfilesForPackage(ContentPackage package)
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        _base.CheckDisposed();

        if (!_packageProfilesReverseLookup.TryGetValue(package, out var keys) || keys.IsEmpty)
            return FluentResults.Result.Fail($"No profiles found for package {package.Name}");

        return _configProfiles.Where(kvp => keys.Contains(kvp.Key)).ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public IReadOnlyDictionary<(ContentPackage Package, string Name), IConfigBase> GetAllConfigs()
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        _base.CheckDisposed();

        return _configs.ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public bool TryGetConfig<T>(ContentPackage package, string name, out T config) where T : IConfigBase
    {
        using var lck = _disposeOpsLock.AcquireReaderLock().GetAwaiter().GetResult();
        _base.CheckDisposed();
        
        config = default;
        if (!_configs.TryGetValue((package, name), out var value))
            return false;
        try
        {
            config = (T)value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<FluentResults.Result> SaveAllConfigs()
    {
        using var lck = await _disposeOpsLock.AcquireReaderLock();
        _base.CheckDisposed();
        if (_configs.IsEmpty)
            return FluentResults.Result.Ok();
        var toSave = _configs.Where(kvp => kvp.Value is not null).Select(kvp => kvp.Value)
            .ToImmutableArray();
        var errList = ImmutableArray.CreateBuilder<IError>();
        foreach (var config in toSave)
        {
            var res = await SaveConfigInternal(config);
            if (res.Errors.Any())
                errList.AddRange(res.Errors);
        }
        return FluentResults.Result.Ok().WithErrors(errList.MoveToImmutable());
    }

    public async Task<FluentResults.Result> SaveConfigsForPackage(ContentPackage package)
    {
        if (package is null)
            return FluentResults.Result.Fail($"{nameof(SaveConfigsForPackage)}: Package was null.");
        using var lck = await _disposeOpsLock.AcquireReaderLock();
        _base.CheckDisposed();
        if (!_packageConfigReverseLookup.TryGetValue(package, out var keys) || keys.IsEmpty)
            return FluentResults.Result.Fail($"No configs found for package {package.Name}");
        ConcurrentQueue<IConfigBase> toSave = new();
        foreach (var key in keys)
        {
            if (_configs.TryGetValue(key, out var config))
                toSave.Enqueue(config);
        }
        if (toSave.IsEmpty)
            return FluentResults.Result.Fail($"No configs found for package {package.Name}");
        var errList = ImmutableArray.CreateBuilder<IError>();
        while (toSave.TryDequeue(out var config))
        {
            var res = await SaveConfigInternal(config);
            if (res.Errors.Any())
                errList.AddRange(res.Errors);
        }
        return FluentResults.Result.Ok().WithErrors(errList.MoveToImmutable());
    }

    public async Task<FluentResults.Result> SaveConfig((ContentPackage Package, string ConfigName) config)
    {
        if (config.Package is null || config.ConfigName.IsNullOrWhiteSpace())
            return FluentResults.Result.Fail($"{nameof(SaveConfig)}: Config properties were null or empty.");
        using var lck = await _disposeOpsLock.AcquireReaderLock();
        _base.CheckDisposed();
        if (!_configs.TryGetValue(config, out var instance))
            return FluentResults.Result.Fail($"{nameof(SaveConfig)}: No config found for package {config.Package.Name} and name {config.ConfigName}");
        return await SaveConfigInternal(instance);
    }

    private void SaveConfigEvent(IConfigBase instance)
    {
        using var lck = _disposeOpsLock.AcquireWriterLock().GetAwaiter().GetResult();
        _base.CheckDisposed();
        SaveConfigInternal(instance).GetAwaiter().GetResult();
    }
    
    private async Task<FluentResults.Result> SaveConfigInternal(IConfigBase instance)
    {
        var localStorePath = Path.Combine("Config", SanitizedFileName($"{instance.OwnerPackage.Name}.xml)"));
        // Locking and checks must be handled by the caller.
        var val = instance.GetSerializableValue();
        var docRes = await _storageService.Value.LoadLocalXmlAsync(instance.OwnerPackage, localStorePath);
        XDocument doc;
        XElement cfgElement;
        XElement valueElement;
        
        // structure is
        /*
         * <Config ContentPackage="[PackageName]">
         *      <[instance.InternalName]>
         *          <Value>
         *              <--Contents Here->
         *          </Value>
         *      </[instance.InternalName]>
         * </Config>
         */
        
        if (docRes.IsFailed || docRes.Value is null)
        {
            doc = new XDocument(
                new XElement("Config", new XAttribute("ContentPackage", instance.OwnerPackage.Name), 
                    cfgElement = new XElement(instance.InternalName, valueElement = new XElement("Value"))));
        }
        else
        {
            doc = docRes.Value;
            var e1 = doc.GetChildElement("Config");
            if (e1 is null)
            {
                e1 = new XElement("Config");
                doc.Add(e1);
            }
            
            cfgElement = e1.GetChildElement(instance.InternalName);
            if (cfgElement is null)
            {
                cfgElement = new XElement(instance.InternalName);
                e1.Add(cfgElement);
            }

            valueElement = cfgElement.GetChildElement("Value");
            if (valueElement is null)
            {
                valueElement = new XElement("Value");
                cfgElement.Add(valueElement);
            }
        }
        
        valueElement.Remove();  // remove from cfg
        
        // get potential updated element
        var updatedElement = val.Match<XElement>(str =>
        {
            valueElement.RemoveAll();
            valueElement.Value = str;
            return valueElement;
        }, element =>
        {
            valueElement.RemoveAll();
            valueElement.Add(element);
            return valueElement;
        });
        
        // (re) add updated element.
        cfgElement.Add(updatedElement);
       
        return await _storageService.Value.SaveLocalXmlAsync(instance.OwnerPackage, localStorePath, doc);
    }
    
    private static readonly Regex RemoveInvalidChars = new Regex($"[{Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()))}]",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private string SanitizedFileName(string fileName, string replacement = "_")
    {
        return RemoveInvalidChars.Replace(fileName, replacement);
    }
    
    
}
