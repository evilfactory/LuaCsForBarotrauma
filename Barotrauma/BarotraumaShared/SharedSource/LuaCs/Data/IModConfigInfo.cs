using System.Collections.Immutable;
using System.Xml.Linq;

namespace Barotrauma.LuaCs.Data;

public partial interface IModConfigInfo : IAssembliesResourcesInfo, 
    ILuaScriptsResourcesInfo, IConfigsResourcesInfo,
    IConfigProfilesResourcesInfo
{
    // package info
    ContentPackage Package { get; }
}

public record ResourceParserInfo(
    ContentPackage Owner,
    XElement Element,
    ImmutableArray<Identifier> Required,
    ImmutableArray<Identifier> Incompatible);
