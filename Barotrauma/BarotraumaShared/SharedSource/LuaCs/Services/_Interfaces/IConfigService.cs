using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.LuaCs.Services.Safe;
using Barotrauma.Networking;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public partial interface IConfigService : IReusableService, ILuaConfigService
{
    /// <summary>
    /// Registers a type initializer from instancing config types by indicated type from config.
    /// </summary>
    /// <param name="initializer"></param>
    /// <typeparam name="TData">The <see cref="Type"/> as parsed from the configuration info.</typeparam>
    /// <typeparam name="TConfig">The resulting configuration instance.</typeparam>
    void RegisterTypeInitializer<TData, TConfig>(IConfigTypeInitializer<TData, TConfig> initializer)
        where TData : IEquatable<TData> where TConfig : IConfigBase;
    
    // Config Files/Resources
    Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources);
    Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigProfileResourceInfo> configProfileResources);
    
    // Immediate Mode
    FluentResults.Result<IConfigEntry<T>> AddConfigEntry<T>(ContentPackage package, string name,
        T defaultValue,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<T, bool> valueChangePredicate = null,
        Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>;

    FluentResults.Result<IConfigList> AddConfigList(ContentPackage package, string name,
        int defaultIndex, IReadOnlyList<string> values,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<IConfigList, int, bool> valueChangePredicate = null,
        Action<IConfigList, int> onValueChanged = null);
    
    FluentResults.Result<IConfigRangeEntry<T>> AddConfigRangeEntry<T>(ContentPackage package, string name,
        T defaultValue, T minValue, T maxValue,
        Func<IConfigRangeEntry<T>, int> getStepCount,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<T, bool> valueChangePredicate = null,
        Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>;
    
    // Utility
    FluentResults.Result ApplyProfileSettings(ContentPackage package, string profileName);
    FluentResults.Result DisposePackageData(ContentPackage package);
    FluentResults.Result<IReadOnlyDictionary<string, IConfigBase>> GetConfigsForPackage(ContentPackage package);
    FluentResults.Result<IReadOnlyDictionary<string, IConfigBase>> GetConfigsForPackage(string packageName);
    IReadOnlyDictionary<(ContentPackage, string), IConfigBase> GetAllConfigs();
    T GetConfig<T>(ContentPackage package, string name) where T : IConfigBase;
    T GetConfig<T>(string packageName, string name) where T : IConfigBase;
}

public interface IConfigTypeInitializer<TData, TConfig> where TData : IEquatable<TData> where TConfig : IConfigBase
{
    FluentResults.Result<TConfig> GetConfig(IConfigInfo configInfo);
}
