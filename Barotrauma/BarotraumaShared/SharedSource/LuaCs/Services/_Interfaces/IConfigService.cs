using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.LuaCs.Services.Safe;
using Barotrauma.Networking;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public partial interface IConfigService : IReusableService, ILuaConfigService
{
    void RegisterSettingTypeInitializer<T>(string typeIdentifier, Func<IConfigInfo, T> settingFactory)
        where T : class, ISettingBase;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="configResources"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NullReferenceException"></exception>
    /// <returns></returns>
    Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources);
    Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigResourceInfo> configProfileResources);
    FluentResults.Result DisposePackageData(ContentPackage package);
    FluentResults.Result DisposeAllPackageData();
    bool TryGetConfig<T>(ContentPackage package, string internalName, out T instance) where T : ISettingBase;
}
