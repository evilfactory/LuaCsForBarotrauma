using System;
using Barotrauma.LuaCs.Networking;

namespace Barotrauma.LuaCs.Configuration;

public interface IConfigEntry<T> : IConfigBase, INetVar where T : IConvertible, IEquatable<T>
{
    T Value { get; }
    bool TrySetValue(T value);
    bool IsAssignable(T value);
    void Initialize(IVarId id, T defaultValue);
}
