using System;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.Networking;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public partial class ConfigService
{
    public ImmutableArray<IDisplayableConfigBase> GetDisplayableConfigs()
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<IDisplayableConfigBase> GetDisplayableConfigsForPackage(ContentPackage package)
    {
        throw new NotImplementedException();
    }

    public Result<IConfigControl> AddConfigControl(ContentPackage package, string name, KeyOrMouse defaultValue, NetSync syncMode = NetSync.None,
        ClientPermissions permissions = ClientPermissions.None, Func<KeyOrMouse, bool> valueChangePredicate = null,
        Action<IConfigControl> onValueChanged = null)
    {
        throw new NotImplementedException();
    }
}
