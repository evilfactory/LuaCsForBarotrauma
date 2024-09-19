using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

public interface IConfigService : IService
{
    /*
     * Resource Files.
     */
    bool TryAddConfigs(ImmutableArray<IConfigResourceInfo> configResources);
    bool TryAddConfigsProfiles(ImmutableArray<IConfigProfileResourceInfo> configProfileResources);
    void RemoveConfigs(ImmutableArray<IConfigResourceInfo> configResources);
    void RemoveConfigsProfiles(ImmutableArray<IConfigProfileResourceInfo> configProfilesResources);
    
    
    /*
     * Already processed
     */
    bool TryAddConfigs(ImmutableArray<IConfigInfo> configs);
    bool TryAddConfigsProfiles(ImmutableArray<IConfigProfileInfo> configProfiles);
    void RemoveConfigs(ImmutableArray<IConfigInfo> configs);
    void RemoveConfigsProfiles(ImmutableArray<IConfigProfileInfo> configProfiles);
    
    /*
     * Immediate mode, does not have displayable functionality
     */
    IConfigEntry<T> AddConfigEntry<T>(ContentPackage package, string name,
        T defaultValue,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<T, bool> valueChangePredicate = null,
        Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>;

    IConfigList AddConfigList(ContentPackage package, string name,
        int defaultIndex, IReadOnlyList<string> values,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<IConfigList, int, bool> valueChangePredicate = null,
        Action<IConfigList, int> onValueChanged = null);
    
    IReadOnlyDictionary<string, IConfigBase> GetConfigsForPackage(ContentPackage package);
    IReadOnlyDictionary<string, IConfigBase> GetConfigsForPackage(string packageName);
    IReadOnlyDictionary<(ContentPackage, string), IConfigBase> GetAllConfigs();
}
