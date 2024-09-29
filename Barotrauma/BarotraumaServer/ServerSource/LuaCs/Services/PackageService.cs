using System;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService
{
    public PackageService(
        Lazy<IXmlModConfigConverterService> converterService, 
        Lazy<IXmlLegacyModConfigConverterService> legacyConfigConverterService,
        Lazy<IXmlLocalizationResConverterService> localizationConverterService,
        IStorageService storageService,
        ILoggerService loggerService)
    {
        _modConfigConverterService = converterService;
        _legacyConfigConverterService = legacyConfigConverterService;
        _localizationConverterService = localizationConverterService;
        _storageService = storageService;
        _loggerService = loggerService;
    }
    // No implementation
    partial void TryParsePackageClient(ContentPackage package) {}
}
