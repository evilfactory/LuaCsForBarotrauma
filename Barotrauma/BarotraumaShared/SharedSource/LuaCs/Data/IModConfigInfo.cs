using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public partial interface IModConfigInfo : IPackageDependenciesInfo, IResourceCultureInfo, IAssembliesResourcesInfo, ILocalizationsResourcesInfo, ILuaScriptsResourcesInfo
{
    // package info
    ContentPackage Package { get; }
    string PackageName { get; }
    
    
    // configuration
    TargetRunMode RunModes { get; }
}
