using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.Networking;
using FluentResults;
using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Services;

public partial class ConfigService : IConfigService
{
    //--- Internals
    private readonly IService _base;
    private int _isDisposed = 0;
    
    public ConfigService()
    {
        this._base = this;
    }
    
    //--- GC
    public bool IsDisposed => ModUtils.Threading.GetBool(ref _isDisposed);

    public void Dispose()
    {
        ModUtils.Threading.SetBool(ref _isDisposed, true);
        throw new NotImplementedException();
    }
    
    public FluentResults.Result Reset()
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    //--- API contracts
    
    #region LuaInterface

    public bool GetConfigBool(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public int GetConfigInt(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public float GetConfigFloat(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public double GetConfigNumber(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public string GetConfigString(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public Vector2 GetConfigVector2(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public Vector3 GetConfigVector3(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public Color GetConfigColor(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public string GetConfigList(string packageName, string configName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigBool(string packageName, string configName, bool value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigInt(string packageName, string configName, int value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigFloat(string packageName, string configName, float value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigNumber(string packageName, string configName, double value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigString(string packageName, string configName, string value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigVector2(string packageName, string configName, Vector2 value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigVector3(string packageName, string configName, Vector3 value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigColor(string packageName, string configName, Color value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public void SetConfigList(string packageName, string configName, string value)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    #endregion

    public void RegisterTypeInitializer<TData, TConfig>(IConfigTypeInitializer<TData, TConfig> initializer) where TData : IEquatable<TData> where TConfig : IConfigBase
    {
        throw new NotImplementedException();
    }
    
    public async Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigProfileResourceInfo> configProfileResources)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public Result<IConfigEntry<T>> AddConfigEntry<T>(ContentPackage package, string name, T defaultValue, NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None, Func<T, bool> valueChangePredicate = null,
        Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public Result<IConfigList> AddConfigList(ContentPackage package, string name, int defaultIndex, IReadOnlyList<string> values,
        NetSync syncMode = NetSync.None, ClientPermissions permissions = ClientPermissions.None,
        Func<IConfigList, int, bool> valueChangePredicate = null, Action<IConfigList, int> onValueChanged = null)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public Result<IConfigRangeEntry<T>> AddConfigRangeEntry<T>(ContentPackage package, string name, T defaultValue, T minValue, T maxValue,
        Func<IConfigRangeEntry<T>, int> getStepCount, NetSync syncMode = NetSync.None, ClientPermissions permissions = ClientPermissions.None,
        Func<T, bool> valueChangePredicate = null, Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public FluentResults.Result ApplyProfileSettings(ContentPackage package, string profileName)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result DisposePackageData(ContentPackage package)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public Result<IReadOnlyDictionary<string, IConfigBase>> GetConfigsForPackage(ContentPackage package)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public Result<IReadOnlyDictionary<string, IConfigBase>> GetConfigsForPackage(string packageName)
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public IReadOnlyDictionary<(ContentPackage, string), IConfigBase> GetAllConfigs()
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public T GetConfig<T>(ContentPackage package, string name) where T : IConfigBase
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }

    public T GetConfig<T>(string packageName, string name) where T : IConfigBase
    {
        _base.CheckDisposed();
        throw new NotImplementedException();
    }
}
