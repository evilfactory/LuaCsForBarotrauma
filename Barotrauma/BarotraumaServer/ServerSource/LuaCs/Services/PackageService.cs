using System;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService
{
    public PackageService(
        Lazy<IXmlModConfigConverterService> converterService, 
        Lazy<ILegacyConfigService> legacyConfigService,
        Lazy<IXmlLocalizationResConverterService> localizationConverterService,
        IStorageService storageService,
        ILoggerService loggerService)
    {
        _modConfigConverterService = converterService;
        _legacyConfigService = legacyConfigService;
        _localizationConverterService = localizationConverterService;
        _storageService = storageService;
        _loggerService = loggerService;
    }
    // No implementation
    partial void TryParsePackageClient(ContentPackage package) {}
}
