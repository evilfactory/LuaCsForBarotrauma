using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
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
    Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources);
    Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigProfileResourceInfo> configProfileResources);
    FluentResults.Result DisposeConfigs(ImmutableArray<IConfigResourceInfo> configResources);
    FluentResults.Result DisposeConfigsProfiles(ImmutableArray<IConfigProfileResourceInfo> configProfilesResources);
    FluentResults.Result DisposeConfigs(ContentPackage package);
    FluentResults.Result DisposeConfigsProfiles(ContentPackage package);
    
    
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
    T GetConfig<T>(ContentPackage package, string name) where T : IConfigBase;
    T GetConfig<T>(string packageName, string name) where T : IConfigBase;
}
