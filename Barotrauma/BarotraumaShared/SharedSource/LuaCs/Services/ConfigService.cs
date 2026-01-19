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
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public bool IsDisposed { get; }
    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }

    #region LuaInterface

    public bool TryGetConfigBool(string packageName, string configName, out bool value)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfigInt(string packageName, string configName, out int value)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfigFloat(string packageName, string configName, out float value)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfigNumber(string packageName, string configName, out double value)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfigString(string packageName, string configName, out string value)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfigVector2(string packageName, string configName, out Vector2 value)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfigVector3(string packageName, string configName, out Vector3 value)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfigColor(string packageName, string configName, out Color value)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfigList(string packageName, string configName, out IReadOnlyList<string> value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigBool(string packageName, string configName, bool value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigInt(string packageName, string configName, int value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigFloat(string packageName, string configName, float value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigNumber(string packageName, string configName, double value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigString(string packageName, string configName, string value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigVector2(string packageName, string configName, Vector2 value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigVector3(string packageName, string configName, Vector3 value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigColor(string packageName, string configName, Color value)
    {
        throw new NotImplementedException();
    }

    public void SetConfigList(string packageName, string configName, string value)
    {
        throw new NotImplementedException();
    }

    public bool TryApplyProfileSettings(string packageName, string profileName)
    {
        throw new NotImplementedException();
    }
    

    #endregion
    
    public void RegisterTypeInitializer<TData, TConfig>(Func<IConfigInfo, Result<TConfig>> initializer, bool replaceIfExists = false) where TData : IEquatable<TData> where TConfig : IConfigBase
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources)
    {
#if DEBUG
        return FluentResults.Result.Ok();    // just for startup testing
#endif
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigResourceInfo> configProfileResources)
    {
#if DEBUG
        return FluentResults.Result.Ok();    // just for startup testing
#endif
        throw new NotImplementedException();
    }

    public Result<TConfig> AddConfig<TConfig>(IConfigInfo configInfo) where TConfig : IConfigBase
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result ApplyProfileSettings(ContentPackage package, string profileName)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result DisposePackageData(ContentPackage package)
    {
#if DEBUG
        return FluentResults.Result.Ok();    // just for startup testing
#endif
        throw new NotImplementedException();
    }

    public FluentResults.Result DisposeAllPackageData()
    {
#if DEBUG
        return FluentResults.Result.Ok();    // just for startup testing
#endif
        throw new NotImplementedException();
    }

    public Result<IReadOnlyDictionary<(ContentPackage Package, string ConfigName), IConfigBase>> GetConfigsForPackage(ContentPackage package)
    {
        throw new NotImplementedException();
    }

    public Result<IReadOnlyDictionary<(ContentPackage Package, string ConfigName), ImmutableArray<(string ConfigName, OneOf<string, XElement> Value)>>> GetProfilesForPackage(ContentPackage package)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyDictionary<(ContentPackage Package, string Name), IConfigBase> GetAllConfigs()
    {
        throw new NotImplementedException();
    }

    public bool TryGetConfig<T>(ContentPackage package, string name, out T config) where T : IConfigBase
    {
#if DEBUG
        config = default(T);
        return true;    // just for startup testing
#endif
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> SaveAllConfigs()
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> SaveConfigsForPackage(ContentPackage package)
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> SaveConfig((ContentPackage Package, string ConfigName) config)
    {
        throw new NotImplementedException();
    }
}
