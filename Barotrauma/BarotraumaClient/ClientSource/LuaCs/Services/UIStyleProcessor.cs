using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Barotrauma.Extensions;

namespace Barotrauma.LuaCs.Services;

public class UIStyleProcessor : HashlessFile
{
    private readonly UIStyleFile _fake;
    public readonly Dictionary<string, GUIFont> Fonts = new();
    public readonly Dictionary<string, GUISprite> Sprites  = new();
    public readonly Dictionary<string, GUISpriteSheet> SpriteSheets = new();
    public readonly Dictionary<string, GUICursor> Cursors = new();
    public readonly Dictionary<string, GUIColor> Colors = new();
    
    public UIStyleProcessor(ContentPackage contentPackage, ContentPath path) : base(contentPackage, path)
    {
        _fake = new UIStyleFile(contentPackage, path);
    }

    public override void LoadFile()
    {
        var element = XMLExtensions.TryLoadXml(path: Path)?.Root?.FromPackage(ContentPackage);
        if (element is null)
            throw new InvalidDataException($"UIStyleProcessor: Failed to load UI style file: {Path}");
        
        var styleElement = element.Name.LocalName.ToLowerInvariant() == "style" ? element : element.GetChildElement("style");
        if (styleElement is null)
            throw new InvalidDataException($"UIStyleProcessor: no 'style' XmlElement found in file: {Path}");
        
        var childElements = styleElement.GetChildElements("Font");
        if (childElements is not null)
            AddToList<GUIFont, GUIFontPrefab>(Fonts, childElements, _fake);

        childElements = styleElement.GetChildElements("Sprite");
        if (childElements is not null)
            AddToList<GUISprite, GUISpritePrefab>(Sprites, childElements, _fake);
        
        childElements = styleElement.GetChildElements("Spritesheet");
        if (childElements is not null)
            AddToList<GUISpriteSheet, GUISpriteSheetPrefab>(SpriteSheets, childElements, _fake);
        
        childElements = styleElement.GetChildElements("Cursor");
        if (childElements is not null)
            AddToList<GUICursor, GUICursorPrefab>(Cursors, childElements, _fake);
        
        childElements = styleElement.GetChildElements("Color");
        if (childElements is not null)
            AddToList<GUIColor, GUIColorPrefab>(Colors, childElements, _fake);


        void AddToList<T1, T2>(Dictionary<string, T1> dict, IEnumerable<ContentXElement> ele, UIStyleFile file) where T1 : GUISelector<T2> where T2 : GUIPrefab
        {
            foreach (ContentXElement prefabElement in ele)
            {
                string name = prefabElement.GetAttributeString("name", string.Empty);
                if (name != string.Empty)
                {
                    var prefab = (T2)Activator.CreateInstance(typeof(T2), new object[]{ prefabElement, file })!;
                    if (!dict.ContainsKey(name))
                        dict[name] = (T1)Activator.CreateInstance(typeof(T1), new object[] { name })!;
                    dict[name].Prefabs.Add(prefab, false);
                }
            }
        }
    }

    public override void UnloadFile()
    {
        Fonts.Values.ForEach(p => p.Prefabs.RemoveByFile(_fake));
        Sprites.Values.ForEach(p => p.Prefabs.RemoveByFile(_fake));
        SpriteSheets.Values.ForEach(p => p.Prefabs.RemoveByFile(_fake));
        Cursors.Values.ForEach(p => p.Prefabs.RemoveByFile(_fake));
        Colors.Values.ForEach(p => p.Prefabs.RemoveByFile(_fake));
        
        Fonts.Clear();
        Sprites.Clear();
        SpriteSheets.Clear();
        Cursors.Clear();
        Colors.Clear();
    }

    public override void Sort()
    {
        Fonts.Values.ForEach(p => p.Prefabs.Sort());
        Sprites.Values.ForEach(p => p.Prefabs.Sort());
        SpriteSheets.Values.ForEach(p => p.Prefabs.Sort());
        Cursors.Values.ForEach(p => p.Prefabs.Sort());
        Colors.Values.ForEach(p => p.Prefabs.Sort());
    }
}
