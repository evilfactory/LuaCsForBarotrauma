﻿using System.Collections.Generic;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services.Processing;

#region TypeDef

public interface IConverterService<TSrc, TOut> : IService
{
    bool TryParseResource(in TSrc src, out TOut resources);
    bool TryParseResources(in IEnumerable<TSrc> sources, out List<TOut> resources);
}

public interface IXmlResourceConverterService<TOut> : IConverterService<XElement, TOut> { }
public interface IResourceToXmlConverterService<TSrc> : IConverterService<TSrc, XElement> { } 

#endregion

/// <summary>
/// Parses Xml to produce loading metadata info for linked loadable files.
/// </summary>
#region XmlToResourceInfoParsers

public interface IXmlAssemblyResConverter : IXmlResourceConverterService<IAssemblyResourceInfo> { }
public interface IXmlConfigResConverterService : IXmlResourceConverterService<IConfigResourceInfo> { }
public interface IXmlLocalizationResConverterService : IXmlResourceConverterService<ILocalizationResourceInfo> { }

#endregion

/// <summary>
/// Parses Xml to produce ready-to-use info/data without any additional file/data loading.
/// </summary>
#region XmlToInfoParsers
public interface IXmlDependencyConverterService : IXmlResourceConverterService<IPackageDependencyInfo> { }
public interface IXmlModConfigConverterService : IXmlResourceConverterService<IModConfigInfo> { }
/// <summary>
/// Parses legacy packages that make use of the RunConfig.xml structure to produce a ModConfig.
/// </summary>
public interface IXmlLegacyModConfigConverterService : IXmlResourceConverterService<IModConfigInfo> { }

#endregion

/// <summary>
/// 
/// </summary>
#region ResToInfoParsers

public interface IConfigResConverterService : IConverterService<IConfigResourceInfo, IConfigInfo> { }
public interface IConfigProfileResConverterService : IConverterService<IConfigProfileResourceInfo, IConfigProfileInfo> { }

#endregion