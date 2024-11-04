using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Configuration;

public record DisplayableData : IDisplayableData
{
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    public string FallbackPackageName { get; init; }
    public string DisplayName { get; init; }
    public string DisplayModName { get; init; }
    public string DisplayCategory { get; init; }
    public string Tooltip { get; init; }
    public string ImageIcon { get; init; }
    public Point IconResolution { get; init; }
    public bool ShowWhenNotLoaded { get; init; }
    public string Description { get; init; }
}
