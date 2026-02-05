using System;

namespace Barotrauma.LuaCs.Configuration;

public interface ISettingControl : ISettingBase
{
    event Action<ISettingControl> OnDown;
    KeyOrMouse Value { get; }
    bool TrySetValue(KeyOrMouse value);
    bool IsDown();
}
