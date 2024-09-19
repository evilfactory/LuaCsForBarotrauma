namespace Barotrauma.LuaCs.Services;

public interface IXmlAssetService : IService
{
    /// <summary>
    /// Tries to load the styles file for the given contentpackage and path into a new UIStylesProcessor instance.
    /// </summary>
    /// <param name="package"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    bool TryLoadStylesFile(ContentPackage package, ContentPath path);
    /// <summary>
    /// Unloads all styles assets and UIStyleProcessor instances.
    /// </summary>
    void UnloadAllStyles();
    
    /// <summary>
    /// Tries to the get the font asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="fontName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUIFont GetFont(string fontName);
    /// <summary>
    /// Tries to the get the sprite asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="spriteName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUISprite GetSprite(string spriteName);
    /// <summary>
    /// Tries to the get the sprite sheet asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="spriteSheetName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUISpriteSheet GetSpriteSheet(string spriteSheetName);
    /// <summary>
    /// Tries to the get the cursor asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="cursorName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUICursor GetCursor(string cursorName);
    /// <summary>
    /// Tries to the get the color asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="colorName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUIColor GetColor(string colorName);
}
