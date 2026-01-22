using System;
using Barotrauma.LuaCs.Services;

namespace Barotrauma.LuaCs.Configuration;

public interface ISettingEntry<T> : ISettingBase, INetworkSyncEntity where T : IEquatable<T>
{
    T Value { get; }
    bool TrySetValue(T value);
    bool IsAssignable(T value);
    new event Action<ISettingEntry<T>> OnValueChanged;
}
