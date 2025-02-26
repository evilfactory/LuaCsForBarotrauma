using System.Collections.Generic;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageManagementService
{
    public PackageManagementService(
        IConverterServiceAsync<ContentPackage, IModConfigInfo> modConfigParserService, 
        IProcessorService<IReadOnlyList<IAssemblyResourceInfo>, IAssembliesResourcesInfo> assemblyInfoConverter, 
        IProcessorService<IReadOnlyList<IConfigResourceInfo>, IConfigsResourcesInfo> configsInfoConverter, 
        IProcessorService<IReadOnlyList<IConfigProfileResourceInfo>, IConfigProfilesResourcesInfo> configProfilesConverter, 
        IProcessorService<IReadOnlyList<ILocalizationResourceInfo>, ILocalizationsResourcesInfo> localizationsConverter, 
        IProcessorService<IReadOnlyList<ILuaScriptResourceInfo>, ILuaScriptsResourcesInfo> luaScriptsConverter, 
        IPackageInfoLookupService packageInfoLookupService)
    {
        _modConfigParserService = modConfigParserService;
        _assemblyInfoConverter = assemblyInfoConverter;
        _configsInfoConverter = configsInfoConverter;
        _configProfilesConverter = configProfilesConverter;
        _localizationsConverter = localizationsConverter;
        _luaScriptsConverter = luaScriptsConverter;
        _packageInfoLookupService = packageInfoLookupService;
    }
}
