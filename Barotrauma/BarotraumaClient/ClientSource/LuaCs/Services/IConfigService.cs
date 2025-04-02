using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

public partial interface IConfigService
{
    ImmutableArray<IDisplayableConfigBase> GetDisplayableConfigs();
    ImmutableArray<IDisplayableConfigBase> GetDisplayableConfigsForPackage(ContentPackage package);
    
    FluentResults.Result<IConfigControl> AddConfigControl(ContentPackage package, string name, KeyOrMouse defaultValue,
        NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None,
        Func<KeyOrMouse, bool> valueChangePredicate = null,
        Action<IConfigControl> onValueChanged = null);
}
