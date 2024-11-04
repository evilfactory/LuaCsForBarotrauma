using System;
using Barotrauma.LuaCs.Data;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Configuration;

public partial interface IConfigBase : IVarId
{
    bool IsInitialized { get; }
    string GetValue();
    bool TrySetValue(string value);
    bool IsAssignable(string value);
    Type GetValueType();
    void Initialize(IVarId id, string defaultValue);
}

public interface IVarId : IDataInfo
{
    Guid InstanceId { get; }
}
