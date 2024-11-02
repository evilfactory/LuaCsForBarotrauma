using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService : IStylesResourcesInfo
{
    private readonly Lazy<IStylesService> _stylesService;
    
    public PackageService(
        Lazy<IXmlModConfigConverterService> converterService, 
        Lazy<ILegacyConfigService> legacyConfigService,
        Lazy<ILuaScriptService> luaScriptService,
        Lazy<ILocalizationService> localizationService,
        Lazy<IPluginService> pluginService,
        Lazy<IStylesService> stylesService,
        Lazy<IConfigService> configService,
        IPackageManagementService packageManagementService,
        IStorageService storageService,
        ILoggerService loggerService)
    {
        _modConfigConverterService = converterService;
        _legacyConfigService = legacyConfigService;
        _luaScriptService = luaScriptService;
        _localizationService = localizationService;
        _pluginService = pluginService;
        _stylesService = stylesService;
        _configService = configService;
        _packageManagementService = packageManagementService;
        _storageService = storageService;
        _loggerService = loggerService;
    }

    public ImmutableArray<IStylesResourceInfo> StylesResourceInfos => ModConfigInfo?.StylesResourceInfos ?? ImmutableArray<IStylesResourceInfo>.Empty;

    public void LoadStyles([NotNull]IStylesResourcesInfo stylesInfo)
    {
        throw new NotImplementedException();
    }   
}
