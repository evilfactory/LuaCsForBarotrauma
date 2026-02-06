using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Data;

public partial interface IConfigInfo : IConfigDisplayInfo { }

public interface IConfigDisplayInfo
{
    /// <summary>
    /// Localization Token for display name.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// Localization Token for description.
    /// </summary>
    string Description { get; }
    /// <summary>
    /// The menu category to display under. Used for filtering.
    /// </summary>
    string DisplayCategory { get; }
    /// <summary>
    /// Should this config be displayed in end-user menus.
    /// </summary>
    bool ShowInMenus { get; }
    /// <summary>
    /// User-friendly on-hover tooltip text or Localization Token.
    /// </summary>
    string Tooltip { get; }
    /// <summary>
    /// Icon for display in menus, if available.
    /// </summary>
    ContentPath ImageIconPath { get; }
}
