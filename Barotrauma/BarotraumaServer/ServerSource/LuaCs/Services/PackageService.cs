using System;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.LuaCs.Services.Processing;

// ReSharper disable once CheckNamespace
namespace Barotrauma.LuaCs.Services;

public partial class PackageService
{
    public PackageService(
        Lazy<IModConfigParserService> configParserService,
        Lazy<ILuaScriptService> luaScriptService,
        Lazy<ILocalizationService> localizationService,
        Lazy<IPluginService> pluginService,
        Lazy<IConfigService> configService,
        IPackageManagementService packageManagementService,
        IStorageService storageService,
        ILoggerService loggerService)
    {
        _configParserService = configParserService;
        _luaScriptService = luaScriptService;
        _localizationService = localizationService;
        _pluginService = pluginService;
        _configService = configService;
        _packageManagementService = packageManagementService;
        _storageService = storageService;
        _loggerService = loggerService;
    }
}
