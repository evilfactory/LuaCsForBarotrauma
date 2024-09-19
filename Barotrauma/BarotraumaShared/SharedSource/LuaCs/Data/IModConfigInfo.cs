using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public partial interface IModConfigInfo : IResourceCultureInfo, IAssembliesResourcesInfo, 
    ILocalizationsResourcesInfo, ILuaScriptsResourcesInfo, IConfigsResourcesInfo,
    IConfigProfilesResourcesInfo
{
    // package info
    ContentPackage Package { get; }
    string PackageName { get; }
    // configuration
    TargetRunMode RunModes { get; }
}
