using System.Xml.Linq;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services.Processing;

/// <summary>
/// Parses Xml to produce loading metadata info for linked loadable files.
/// </summary>
#region XmlToResourceInfoParsers

public interface IXmlAssemblyResParser : IResourceParser<XElement, IAssemblyResourceInfo> { }
public interface IXmlConfigResParser : IResourceParser<XElement, IConfigResourceInfo> { }
public interface IXmlLocalizationResParser : IResourceParser<XElement, ILocalizationResourceInfo> { }

#endregion

/// <summary>
/// Parses Xml to produce ready-to-use info/data without any additional file/data loading.
/// </summary>
#region XmlToInfoParsers
public interface IXmlDependencyParser : IResourceParser<XElement, IPackageDependencyInfo> { }
public interface IXmlModConfigParser : IResourceParser<XElement, IModConfigInfo> { }

#endregion




