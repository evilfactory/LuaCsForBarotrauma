using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService : IStylesResourcesInfo
{
    private readonly Lazy<IStylesService> _stylesService;
    public IStylesService Styles => _stylesService.Value;
    
    public PackageService(
        Lazy<IModConfigParserService> configParserService,
        Lazy<ILuaScriptService> luaScriptService,
        Lazy<ILocalizationService> localizationService,
        Lazy<IPluginService> pluginService,
        Lazy<IStylesService> stylesService,
        Lazy<IConfigService> configService,
        IPackageManagementService packageManagementService,
        IStorageService storageService,
        ILoggerService loggerService)
    {
        _configParserService = configParserService;
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

    public FluentResults.Result LoadStyles([NotNull]IStylesResourcesInfo stylesInfo)
    {
        throw new NotImplementedException();
    }   
}
