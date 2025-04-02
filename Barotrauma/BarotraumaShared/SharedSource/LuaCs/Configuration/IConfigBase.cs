using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Configuration;

public partial interface IConfigBase : IEquatable<IConfigBase>
{
    Type GetValueType();
    string GetValue();
    bool TrySetValue(string value);
    bool IsAssignable(string value);
}
