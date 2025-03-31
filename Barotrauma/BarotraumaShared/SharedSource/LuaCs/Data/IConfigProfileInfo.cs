using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.LuaCs.Data;

public interface IConfigProfileInfo : IDataInfo
{
    IReadOnlyList<(string ConfigName, OneOf.OneOf<string, XElement> Value)> ProfileValues { get; }
}
