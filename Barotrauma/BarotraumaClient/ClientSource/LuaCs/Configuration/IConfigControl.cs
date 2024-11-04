using System;

namespace Barotrauma.LuaCs.Configuration;

public interface IConfigControl : IConfigBase
{
    event Action<IConfigControl> OnDown;
    KeyOrMouse Value { get; }
    bool IsAssignable(KeyOrMouse value);
    bool TrySetValue(KeyOrMouse value);
    bool IsDown();
}
