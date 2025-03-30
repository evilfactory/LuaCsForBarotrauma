using System;
using System.Collections.Generic;
using Barotrauma.LuaCs.Services;

namespace Barotrauma.LuaCs.Configuration;

public interface IConfigList<T> : IConfigEntry<T>, INetworkSyncEntity where T : IEquatable<T>
{
    IReadOnlyList<T> Options { get; }
    new event Action<IConfigList<T>> OnValueChanged;
}
