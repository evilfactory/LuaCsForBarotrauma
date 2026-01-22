using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

public partial interface IConfigService
{
    ImmutableArray<IDisplayableConfigBase> GetDisplayableConfigs();
    ImmutableArray<IDisplayableConfigBase> GetDisplayableConfigsForPackage(ContentPackage package);
    
    FluentResults.Result<ISettingControl> AddConfigControl(IConfigInfo configInfo);
}
