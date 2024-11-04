using System;
using System.Collections.Generic;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Networking;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

public partial interface IConfigService
{
    /*
     * Immediate mode
     */
    FluentResults.Result<IConfigEntry<T>> AddConfigEntry<T>(IDisplayableData data,
        T defaultValue,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<T, bool> valueChangePredicate = null,
        Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>;

    FluentResults.Result<IConfigList> AddConfigList(IDisplayableData data,
        int defaultIndex, IReadOnlyList<string> values,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<IConfigList, int, bool> valueChangePredicate = null,
        Action<IConfigList, int> onValueChanged = null);
    
    FluentResults.Result<IConfigRangeEntry<T>> AddConfigRangeEntry<T>(IDisplayableData data,
        T defaultValue, T minValue, T maxValue,
        Func<IConfigRangeEntry<T>, int> getStepCount,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<T, bool> valueChangePredicate = null,
        Action<IConfigEntry<T>> onValueChanged = null) where T : IConvertible, IEquatable<T>;
}
