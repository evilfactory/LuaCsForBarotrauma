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
        if (_storageService.TryLoadPackageXml(package, "ModConfig.xml", out var configXml)
            && configXml.Root is not null)
        {
            if (_modConfigConverterService.Value.TryParseResource(configXml.Root, out IModConfigInfo configInfo))
            {
                ModConfigInfo = configInfo;
            }
            else
            {
                _loggerService.LogError($"Failed to parse ModConfig.xml for package {package.Name}");
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
        
        // load client resources
        TryParsePackageClient(package);
        return true;
    }

    partial void TryParsePackageClient(ContentPackage package);
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}
