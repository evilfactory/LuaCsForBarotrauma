using System;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
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

    public Result<IConfigControl> AddConfigControl(IConfigInfo configInfo)
    {
        throw new NotImplementedException();
    }
}
