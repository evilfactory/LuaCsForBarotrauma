using System;
using Barotrauma.LuaCs.Services;

namespace Barotrauma.LuaCs.Configuration;

public interface IConfigEntry<T> : IConfigBase, INetworkSyncEntity where T : IEquatable<T>
{
    T Value { get; }
    bool TrySetValue(T value);
    bool IsAssignable(T value);
}
