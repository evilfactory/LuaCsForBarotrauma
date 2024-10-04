using System;
using System.Collections.Immutable;
using System.Globalization;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService : IContentPackageService
{
    // mod config / package scanners/parsers
    private readonly Lazy<IXmlModConfigConverterService> _modConfigConverterService;
    private readonly Lazy<ILegacyConfigService> _legacyConfigService;
    private readonly Lazy<ILuaScriptService> _luaScriptService;
    private readonly Lazy<ILocalizationService> _localizationService;
    private readonly Lazy<IPluginService> _pluginService;
    private readonly IPluginManagementService _pluginManagementService;
    private readonly IPackageManagementService _packageManagementService;
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;
    
    // .ctor in server source and client source
    
    public ContentPackage Package { get; private set; }

    #region DataContracts
    
    public IModConfigInfo ModConfigInfo { get; private set; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies => ModConfigInfo?.Dependencies ?? ImmutableArray<IPackageDependencyInfo>.Empty;
    public ImmutableArray<CultureInfo> SupportedCultures => ModConfigInfo?.SupportedCultures ?? ImmutableArray<CultureInfo>.Empty;
    public ImmutableArray<IAssemblyResourceInfo> Assemblies => ModConfigInfo?.Assemblies ?? ImmutableArray<IAssemblyResourceInfo>.Empty;
    public ImmutableArray<ILocalizationResourceInfo> Localizations => ModConfigInfo?.Localizations ?? ImmutableArray<ILocalizationResourceInfo>.Empty;
    public ImmutableArray<ILuaResourceInfo> LuaScripts => ModConfigInfo?.LuaScripts ?? ImmutableArray<ILuaResourceInfo>.Empty;

    #endregion
    
    public bool TryLoadResourcesInfo(ContentPackage package)
    {
        // try loading the ModConfig.xml. If it fails, use the Legacy loader to try and construct one from the package structure.
        if (_storageService.TryLoadPackageXml(package, "ModConfig.xml", out var configXml)
            && configXml.Root is not null)
        {
            if (_modConfigConverterService.Value.TryParseResource(configXml.Root, out IModConfigInfo configInfo))
            {
                ModConfigInfo = configInfo;
            }
            else
            {
                _loggerService.LogError($"Failed to parse ModConfig.xml for package {package.Name}, package mod content not loaded.");
                return false;
            }
        }
        else if (_legacyConfigService.Value.TryBuildModConfigFromLegacy(package, out var legacyConfig))
        {
            ModConfigInfo = legacyConfig;
        }
        else
        {
            // vanilla mod or broken
            return false;
        }

        throw new NotImplementedException();
    }

    public bool TryLoadAssemblies()
    {
        throw new NotImplementedException();
    }

    public bool TryLoadLocalizations()
    {
        throw new NotImplementedException();
    }

    public bool TryLoadLuaScripts()
    {
        throw new NotImplementedException();
    }

    public partial bool TryLoadStyles();

    public bool TryLoadConfig()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    
}
