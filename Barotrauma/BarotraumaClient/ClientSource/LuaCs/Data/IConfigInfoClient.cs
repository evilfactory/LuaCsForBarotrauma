using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Data;

/// <summary>
/// Client-only information for IConfigInfo contract, such as icons and display resources.
/// </summary>
public partial interface IConfigInfo
{
    string Description { get; }
    /// <summary>
    /// Human-friendly name displayed in menus. Internal name will be used if empty.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// What category should this be displayed under in the menus. Overriden when 'ConfigDataType' is 'ControlInput'.
    /// </summary>
    string DisplayCategory { get; }
    /// <summary>
    /// Icon to be displayed next to the setting.
    /// </summary>
    string ImageIcon { get; }
    /// <summary>
    /// Absolute icon size in pixels.
    /// </summary>
    Vector2 IconSize { get; }
    /// <summary>
    /// On hover tooltip.
    /// </summary>
    string Tooltip { get; }
    /// <summary>
    /// Whether the value should be allowed to be shown when mods are not loaded. Used when developers want to hook events
    /// before values are allowed to be changed.
    /// </summary>
    bool HideWhenPackageNotLoaded { get; }
}
