using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace Barotrauma.LuaCs.Data;

public interface IModConfigInfo : IAssembliesResourcesInfo, 
    ILuaScriptsResourcesInfo, IConfigsResourcesInfo
{
    // package info
    ContentPackage Package { get; }
}

public record ResourceParserInfo(
    [NotNull] ContentPackage Owner,
    [NotNull] XElement Element,
    ImmutableArray<Identifier> Required,
    ImmutableArray<Identifier> Incompatible);
