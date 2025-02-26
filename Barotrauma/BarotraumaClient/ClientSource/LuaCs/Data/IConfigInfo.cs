using Barotrauma.LuaCs.Configuration;

namespace Barotrauma.LuaCs.Data;

public partial interface IConfigInfo
{
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
