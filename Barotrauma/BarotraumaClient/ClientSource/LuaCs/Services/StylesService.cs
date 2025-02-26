using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentResults;
using FluentResults.LuaCs;

namespace Barotrauma.LuaCs.Services;

// TODO: Complete rewrite
public class StylesService : IStylesService
{
    private readonly ConcurrentDictionary<string, UIStyleProcessor> _loadedProcessors = new();
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;

    public StylesService(IStorageService storageService, ILoggerService loggerService)
    {
        _storageService = storageService;
        _loggerService = loggerService;
    }


    public async Task<FluentResults.Result> LoadStylesFileAsync(ContentPackage package, ContentPath path)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result UnloadAllStyles()
    {
        if (NoProcessorsLoaded)
            return FluentResults.Result.Fail(new Error($"{nameof(StylesService)}.{nameof(UnloadAllStyles)}: No processors have been loaded.")
                .WithMetadata(MetadataType.ExceptionObject, this));
        
        foreach (var processor in _loadedProcessors)
        {
            processor.Value.UnloadFile();
        }
        _loadedProcessors.Clear();
        return FluentResults.Result.Ok();
    }

    public GUIFont GetFont(string fontName)
    {
        if (NoProcessorsLoaded)
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
        if (NoProcessorsLoaded)
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
        if (NoProcessorsLoaded)
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
        if (NoProcessorsLoaded)
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
        if (NoProcessorsLoaded)
            return null;
        foreach (var processor in _loadedProcessors.Values)
        {
            if (processor.Colors.TryGetValue(colorName, out var asset))
                return asset;
        }

        return null;
    }

    private bool NoProcessorsLoaded => _loadedProcessors.IsEmpty;

    public void Dispose()
    {
        UnloadAllStyles();
        GC.SuppressFinalize(this);
    }

    public bool IsDisposed { get; private set; }
}
