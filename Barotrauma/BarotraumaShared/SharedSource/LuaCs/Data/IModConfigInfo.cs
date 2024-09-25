using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public interface IModConfigInfo : IPackageDependenciesInfo, IResourceCultureInfo
{
    // package info
    ContentPackage Package { get; }
    string PackageName { get; }
    
    // loadable content metadata
    ImmutableArray<IAssemblyResourceInfo> LoadableAssemblies { get; }
    ImmutableArray<ILocalizationResourceInfo> LocalizationFiles { get; }
    
    // configuration
    TargetRunMode RunModes { get; }
}
