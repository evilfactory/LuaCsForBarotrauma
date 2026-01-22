using System;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Configuration;

public partial interface ISettingBase : IDataInfo, IEquatable<ISettingBase>, IDisposable
{
    Type GetValueType();
    string GetStringValue();
    bool TrySetValue(OneOf.OneOf<string, XElement> value);
    bool IsAssignable(OneOf.OneOf<string, XElement> value);
    event Func<OneOf.OneOf<string, XElement>, bool> IsNewValueValid; 
    event Action<ISettingBase> OnValueChanged;
    OneOf.OneOf<string, XElement> GetSerializableValue();
}
