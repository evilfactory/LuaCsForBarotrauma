using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentResults;
using FluentResults.LuaCs;

namespace Barotrauma.LuaCs.Services;

public class StylesService : IStylesService
{
    private readonly Dictionary<string, UIStyleProcessor> _loadedProcessors = new();
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;

    public StylesService(IStorageService storageService, ILoggerService loggerService)
    {
        _storageService = storageService;
        _loggerService = loggerService;
    }
    
    public FluentResults.Result LoadStylesFile(ContentPackage package, ContentPath path)
    {
        //check if file already in dict
        if (_loadedProcessors.ContainsKey(path.FullPath))
        {
            return FluentResults.Result.Ok();
        }
        //check if file exists
        if (_storageService.FileExists(path.FullPath) is {} result 
            && result.IsFailed | (result.IsSuccess & result.Value == false))
        {
            return FluentResults.Result.Fail(result.Errors)
                .WithError(new Error($"{nameof(StylesService)}.{nameof(LoadStylesFile)} file does not exist!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, package));
        }

        try
        {
            var styleProcessor = new UIStyleProcessor(package, path);
            styleProcessor.LoadFile();
            _loadedProcessors.Add(path.FullPath, styleProcessor);
        }
        catch (InvalidDataException exception)
        {
            return FluentResults.Result.Fail(new Error($"{nameof(StylesService)}.{nameof(LoadStylesFile)} failed for ContentPackage {package.Name}: Exception: {exception.Message}")
                .WithMetadata(MetadataType.ExceptionDetails, exception.Message)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, package)
                .WithMetadata(MetadataType.StackTrace, exception.StackTrace));
        }

        return FluentResults.Result.Ok();
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

    private bool NoProcessorsLoaded => _loadedProcessors.Count < 1;

    public void Dispose()
    {
        UnloadAllStyles();
        GC.SuppressFinalize(this);
    }

    public FluentResults.Result Reset()
    {
        return UnloadAllStyles();
    }
}
