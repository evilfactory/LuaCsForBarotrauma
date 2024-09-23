using System.Xml.Linq;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services.Processing;

#region XmlToResourceParsers

public interface IXmlStylesToResParser : IResourceParser<XElement, IStylesResourceInfo> { }

#endregion
