using System;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Configuration;

public partial interface IConfigBase : IDataInfo, IEquatable<IConfigBase>, IDisposable
{
    Type GetValueType();
    string GetValue();
    bool TrySetValue(OneOf.OneOf<string, XElement> value);
    bool IsAssignable(OneOf.OneOf<string, XElement> value);
}
