using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs;

public partial interface IConfigService
{
    ImmutableArray<IDisplayableConfigBase> GetDisplayableConfigs();
    ImmutableArray<IDisplayableConfigBase> GetDisplayableConfigsForPackage(ContentPackage package);
    
    FluentResults.Result<ISettingControl> AddConfigControl(IConfigInfo configInfo);
}
