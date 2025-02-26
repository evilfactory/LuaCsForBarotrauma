using System.Threading.Tasks;

namespace Barotrauma.LuaCs.Services;

// TODO: Rework interface to support resource infos.
/// <summary>
/// Loads XML Style assets from the given content package.
/// </summary>
public interface IStylesService : IService
{
    /// <summary>
    /// Tries to load the styles file for the given <see cref="ContentPackage"/> and path into a new <see cref="UIStyleProcessor"/> instance.
    /// </summary>
    /// <param name="package"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    Task<FluentResults.Result> LoadStylesFileAsync(ContentPackage package, ContentPath path);
    
    /// <summary>
    /// Unloads all styles assets and <see cref="UIStyleProcessor"/> instances.
    /// </summary>
    FluentResults.Result UnloadAllStyles();
    
    /// <summary>
    /// Tries to the get the <see cref="GUIFont"/> asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="fontName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUIFont GetFont(string fontName);
    
    /// <summary>
    /// Tries to the get the <see cref="GUISprite"/> asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="spriteName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUISprite GetSprite(string spriteName);
    
    /// <summary>
    /// Tries to the get the <see cref="GUISpriteSheet"/> asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="spriteSheetName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUISpriteSheet GetSpriteSheet(string spriteSheetName);
    
    /// <summary>
    /// Tries to the get the <see cref="GUICursor"/> asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="cursorName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUICursor GetCursor(string cursorName);
    
    /// <summary>
    /// Tries to the get the <see cref="GUIColor"/> asset by xml asset name, returns null on failure.
    /// </summary>
    /// <param name="colorName">XML Name of the asset.</param>
    /// <returns>The asset or null if none are found.</returns>
    GUIColor GetColor(string colorName);
}
