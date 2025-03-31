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
    /// <summary>
    /// Registers a type initializer from instancing config types by indicated type from config.
    /// </summary>
    /// <param name="initializer"></param>
    /// <param name="replaceIfExists"></param>
    /// <typeparam name="TData">The <see cref="Type"/> as parsed from the configuration info.</typeparam>
    /// <typeparam name="TConfig">The resulting configuration instance.</typeparam>
    void RegisterTypeInitializer<TData, TConfig>(IConfigTypeInitializer<TConfig> initializer, bool replaceIfExists = false)
        where TData : IEquatable<TData> where TConfig : IConfigBase;
    
    // Config Files/Resources
    Task<FluentResults.Result> LoadConfigsAsync(ImmutableArray<IConfigResourceInfo> configResources);
    Task<FluentResults.Result> LoadConfigsProfilesAsync(ImmutableArray<IConfigProfileResourceInfo> configProfileResources);
    
    // Immediate Mode
    FluentResults.Result<TConfig> AddConfig<TConfig>(IConfigInfo configInfo) where TConfig : IConfigBase;
    
    // Utility
    FluentResults.Result ApplyProfileSettings(ContentPackage package, string profileName);
    FluentResults.Result DisposePackageData(ContentPackage package);
    FluentResults.Result<IReadOnlyDictionary<(ContentPackage Package, string ConfigName), IConfigBase>> GetConfigsForPackage(ContentPackage package);
    FluentResults.Result<IReadOnlyDictionary<(ContentPackage Package, string ConfigName), ImmutableArray<(string ConfigName, OneOf.OneOf<string, XElement> Value)>>> 
        GetProfilesForPackage(ContentPackage package);
    IReadOnlyDictionary<(ContentPackage Package, string Name), IConfigBase> GetAllConfigs();
    bool TryGetConfig<T>(ContentPackage package, string name, out T config) where T : IConfigBase;
}

public interface IConfigTypeInitializer<TConfig> where TConfig : IConfigBase 
{
    FluentResults.Result<TConfig> Initialize(IConfigInfo configInfo);
}
