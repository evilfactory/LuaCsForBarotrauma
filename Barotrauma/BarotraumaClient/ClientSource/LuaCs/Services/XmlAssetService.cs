using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.LuaCs.Services;

public class XmlAssetService : IXmlAssetService
{
    private readonly Dictionary<string, UIStyleProcessor> _loadedProcessors = new();
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;

    public XmlAssetService(IStorageService storageService, ILoggerService loggerService)
    {
        _storageService = storageService;
        _loggerService = loggerService;
    }
    
    public bool TryLoadStylesFile(ContentPackage package, ContentPath path)
    {
        //check if file already in dict
        if (_loadedProcessors.ContainsKey(path.FullPath))
        {
            return true;
        }
        //check if file exists
        if (_storageService.FileExists(path.FullPath))
        {
            try
            {
                var styleProcessor = new UIStyleProcessor(package, path);
                styleProcessor.LoadFile();
                _loadedProcessors.Add(path.FullPath, styleProcessor);
            }
            catch (InvalidDataException exception)
            {
                _loggerService.LogError($"XmlAssetService.TryLoadStylesFile failed for ContentPackage {package.Name}: Exception: {exception.Message}");
                return false;
            }

            return true;
        }

        return false;
    }

    public void UnloadAllStyles()
    {
        if (NoProcessorsLoaded())
            return;
        
        foreach (var processor in _loadedProcessors)
        {
            processor.Value.UnloadFile();
        }
        _loadedProcessors.Clear();
    }

    public GUIFont GetFont(string fontName)
    {
        if (NoProcessorsLoaded())
            return null;
        foreach (var processor in _loadedProcessors.Values)
        {
            if (processor.Fonts.TryGetValue(fontName, out var asset))
                return asset;
        }

        return null;
    }

    public GUISprite GetSprite(string spriteName)
    {
        if (NoProcessorsLoaded())
            return null;
        foreach (var processor in _loadedProcessors.Values)
        {
            if (processor.Sprites.TryGetValue(spriteName, out var asset))
                return asset;
        }

        return null;
    }

    public GUISpriteSheet GetSpriteSheet(string spriteSheetName)
    {
        if (NoProcessorsLoaded())
            return null;
        foreach (var processor in _loadedProcessors.Values)
        {
            if (processor.SpriteSheets.TryGetValue(spriteSheetName, out var asset))
                return asset;
        }

        return null;
    }

    public GUICursor GetCursor(string cursorName)
    {
        if (NoProcessorsLoaded())
            return null;
        foreach (var processor in _loadedProcessors.Values)
        {
            if (processor.Cursors.TryGetValue(cursorName, out var asset))
                return asset;
        }

        return null;
    }

    public GUIColor GetColor(string colorName)
    {
        if (NoProcessorsLoaded())
            return null;
        foreach (var processor in _loadedProcessors.Values)
        {
            if (processor.Colors.TryGetValue(colorName, out var asset))
                return asset;
        }

        return null;
    }

    private bool NoProcessorsLoaded() => _loadedProcessors.Count < 1;

    public void Dispose()
    {
        UnloadAllStyles();
    }
}
