using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs;
using Barotrauma.Networking;
using FluentResults;

namespace Barotrauma.LuaCs;

public partial interface IConfigService : IReusableService, ILuaConfigService
{
    void RegisterSettingTypeInitializer<T>(string typeIdentifier, Func<(IConfigService ConfigService, IConfigInfo Info), T> settingFactory)
        where T : class, ISettingBase;
    Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources);
    Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigResourceInfo> configProfileResources);
    FluentResults.Result DisposePackageData(ContentPackage package);
    FluentResults.Result DisposeAllPackageData();
    bool TryGetConfig<T>(ContentPackage package, string internalName, out T instance) where T : ISettingBase;
}
