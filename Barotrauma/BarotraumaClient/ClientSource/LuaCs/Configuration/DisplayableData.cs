using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Configuration;

public class DisplayableData : IDisplayableData
{
    public string Name { get; private set; }
    public string ModName { get; private set; }
    public string DisplayName { get; private set; }
    public string DisplayModName { get; private set; }
    public string DisplayCategory { get; private set; }
    public string Tooltip { get; private set; }
    public string ImageIcon { get; private set; }
    public Point IconResolution { get; private set; }
    public bool ShowWhenNotLoaded { get; private set; }
}
