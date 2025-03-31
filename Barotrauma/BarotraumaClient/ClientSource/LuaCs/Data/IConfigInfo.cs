using Barotrauma.LuaCs.Configuration;

namespace Barotrauma.LuaCs.Data;

public partial interface IConfigInfo : IConfigDisplayInfo { }

public interface IConfigDisplayInfo
{
    /// <summary>
    /// User-friendly name or Localization Token.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// User-friendly description or Localization Token.
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
    string ImageIconPath { get; }
}
