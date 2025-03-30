using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public partial interface IModConfigInfo : IAssembliesResourcesInfo, 
    ILuaScriptsResourcesInfo, IConfigsResourcesInfo,
    IConfigProfilesResourcesInfo
{
    // package info
    ContentPackage Package { get; }
    string PackageName { get; }
}
