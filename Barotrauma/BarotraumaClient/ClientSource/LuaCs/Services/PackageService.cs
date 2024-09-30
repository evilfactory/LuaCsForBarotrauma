using System;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService
{
    private readonly Lazy<IXmlStylesToResConverterService> _stylesConverterService;
    
    public PackageService(
        Lazy<IXmlModConfigConverterService> converterService, 
        Lazy<ILegacyConfigService> legacyConfigService,
        Lazy<IXmlLocalizationResConverterService> localizationConverterService,
        Lazy<IXmlStylesToResConverterService> stylesConverterService,
        IStorageService storageService,
        ILoggerService loggerService)
    {
        _modConfigConverterService = converterService;
        _legacyConfigService = legacyConfigService;
        _localizationConverterService = localizationConverterService;
        _stylesConverterService = stylesConverterService;
        _storageService = storageService;
        _loggerService = loggerService;
    }
    partial void TryParsePackageClient(ContentPackage package)
    {
        throw new NotImplementedException();
    }
}
