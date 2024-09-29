using System;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService
{
    private readonly Lazy<IXmlStylesToResConverterService> _stylesConverterService;
    
    public PackageService(
        Lazy<IXmlModConfigConverterService> converterService, 
        Lazy<IXmlLegacyModConfigConverterService> legacyConfigConverterService,
        Lazy<IXmlLocalizationResConverterService> localizationConverterService,
        Lazy<IXmlStylesToResConverterService> stylesConverterService,
        IStorageService storageService,
        ILoggerService loggerService)
    {
        _modConfigConverterService = converterService;
        _legacyConfigConverterService = legacyConfigConverterService;
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
