using System.Numerics;
using Barotrauma.LuaCs.Data;
using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Configuration;

/// <summary>
/// Contains the Display Data for use with Menus.
/// </summary>
public interface IDisplayableData : IDataInfo
{
    /// <summary>
    /// The name to display in GUIs and Menus.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// The mod name to display in GUIs and Menus.
    /// </summary>
    string DisplayModName { get; } 
    /// <summary>
    /// Category this instance falls under. Used by menus when filtering by category.
    /// </summary>
    string DisplayCategory { get; } 
    /// <summary>
    /// The tooltip shown on hover.
    /// </summary>
    string Tooltip { get; } 
    /// <summary>
    /// The fully qualified filepath to the image icon for this config.
    /// </summary>
    string ImageIcon { get; } 
    /// <summary>
    /// Required if ImageIcon is set. X,Y resolution of the image.
    /// </summary>
    Point IconResolution { get; }
    /// <summary>
    /// Whether to show the entry in the menu when not loaded.
    /// </summary>
    bool ShowWhenNotLoaded { get; }
    /// <summary>
    /// What does this setting do?
    /// </summary>
    string Description { get; }
}

public interface IDisplayableInitialize
{
    void Initialize(IDisplayableData values);
    
    // copy this as needed
    /*public void Initialize(IDisplayableData values)
    {
        this.InternalName = values.InternalName;
        this.OwnerPackage = values.OwnerPackage;
        this.DisplayName = values.DisplayName;
        this.DisplayModName = values.DisplayModName;
        this.DisplayCategory = values.DisplayCategory;
        this.Tooltip = values.Tooltip;
        this.ImageIcon = values.ImageIcon;
        this.IconResolution = values.IconResolution;
        this.ShowWhenNotLoaded = values.ShowWhenNotLoaded;
    }*/
}
