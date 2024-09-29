using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService : IContentPackageService
{
    private readonly Lazy<IXmlModConfigConverterService> _modConfigConverterService;
    private readonly Lazy<IXmlLegacyModConfigConverterService> _legacyConfigConverterService;
    private readonly Lazy<IXmlLocalizationResConverterService> _localizationConverterService;
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;
    
    // cctor in server source and client source
    
    public ContentPackage Package { get; private set; }
    
    public IModConfigInfo ModConfigInfo { get; private set; }

    public bool TryParsePackage(ContentPackage package)
    {
        // scan for files: modconfig.xml
        
        // on fail, scan and try for legacy loading
        
        // load resources info for: config, assemblies, lua files, localization
        
        // load styles data on clients
        TryParsePackageClient(package);

        throw new NotImplementedException();
    }

    partial void TryParsePackageClient(ContentPackage package);
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}
