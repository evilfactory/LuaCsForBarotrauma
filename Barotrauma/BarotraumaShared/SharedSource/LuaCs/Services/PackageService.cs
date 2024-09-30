using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService : IContentPackageService
{
    private readonly Lazy<IXmlModConfigConverterService> _modConfigConverterService;
    private readonly Lazy<ILegacyConfigService> _legacyConfigService;
    private readonly Lazy<IXmlLocalizationResConverterService> _localizationConverterService;
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;
    
    // cctor in server source and client source
    
    public ContentPackage Package { get; private set; }
    
    public IModConfigInfo ModConfigInfo { get; private set; }

    public bool TryParsePackage(ContentPackage package)
    {
        if (_storageService.TryLoadPackageXml(package, "ModConfig.xml", out var config))
        {
            
            
        }
        else
        {
            if (_legacyConfigService.Value.TryBuildModConfigFromLegacy(package, out var legacyConfig))
            {
                ModConfigInfo = legacyConfig;
                // no support for newer features, end here.
                return true;
            }
            
            // no mod data present, either a vanilla mod or broken.
        }
        
        // load resources info for: config, assemblies, lua files, localization
        
        throw new NotImplementedException();
    }

    partial void TryParsePackageClient(ContentPackage package);
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}
