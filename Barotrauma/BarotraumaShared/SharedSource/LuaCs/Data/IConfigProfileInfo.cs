using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.LuaCs.Data;

public interface IConfigProfileInfo : IDataInfo
{
    IReadOnlyList<(string SettingName, XElement Element)> ProfileValues { get; }
}
