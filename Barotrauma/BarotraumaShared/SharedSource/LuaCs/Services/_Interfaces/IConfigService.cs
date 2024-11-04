using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Networking;
using Barotrauma.LuaCs.Services.Safe;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

public partial interface IConfigService : IReusableService, ILuaConfigService
{
    /*
     * Resource Files.
     */
    FluentResults.Result AddConfigs(ImmutableArray<IConfigResourceInfo> configResources);
    FluentResults.Result AddConfigsProfiles(ImmutableArray<IConfigProfileResourceInfo> configProfileResources);
    FluentResults.Result RemoveConfigs(ImmutableArray<IConfigResourceInfo> configResources);
    FluentResults.Result RemoveConfigsProfiles(ImmutableArray<IConfigProfileResourceInfo> configProfilesResources);
    
    
    /*
     * From resources
     */
    FluentResults.Result AddConfigs(ImmutableArray<IConfigInfo> configs);
    FluentResults.Result AddConfigsProfiles(ImmutableArray<IConfigProfileInfo> configProfiles);
    FluentResults.Result RemoveConfigs(ImmutableArray<IConfigInfo> configs);
    FluentResults.Result RemoveConfigsProfiles(ImmutableArray<IConfigProfileInfo> configProfiles);
    
    /*
     * Immediate mode
     */
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
    
    FluentResults.Result<IConfigEntry<T>> AddConfigEntry<T>(string packageName, string name,
        T defaultValue,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<T, bool> valueChangePredicate = null,
        Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>;

    FluentResults.Result<IConfigList> AddConfigList(string packageName, string name,
        int defaultIndex, IReadOnlyList<string> values,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<IConfigList, int, bool> valueChangePredicate = null,
        Action<IConfigList, int> onValueChanged = null);
    
    FluentResults.Result<IConfigRangeEntry<T>> AddConfigRangeEntry<T>(string packageName, string name,
        T defaultValue, T minValue, T maxValue,
        Func<IConfigRangeEntry<T>, int> getStepCount,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<T, bool> valueChangePredicate = null,
        Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>;
    
    FluentResults.Result<IReadOnlyDictionary<string, IConfigBase>> GetConfigsForPackage(ContentPackage package);
    FluentResults.Result<IReadOnlyDictionary<string, IConfigBase>> GetConfigsForPackage(string packageName);
    IReadOnlyDictionary<(ContentPackage, string), IConfigBase> GetAllConfigs();
    FluentResults.Result<IConfigBase> GetConfig(ContentPackage package, string name);
    FluentResults.Result<IConfigBase> GetConfig(string packageName, string name);
    FluentResults.Result<T> GetConfig<T>(ContentPackage package, string name) where T : IConfigBase;
    FluentResults.Result<T> GetConfig<T>(string packageName, string name) where T : IConfigBase;
}
