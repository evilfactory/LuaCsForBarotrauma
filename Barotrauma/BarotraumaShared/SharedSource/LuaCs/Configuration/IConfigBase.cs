using System;
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

public interface IVarId
{
    Guid InstanceId { get; }
    string InternalName { get; }
    ContentPackage OwnerPackage { get; }
}
