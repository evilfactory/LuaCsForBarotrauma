local defaultLib = {}

local CreateStatic = LuaSetup.LuaUserData.CreateStatic
local CreateEnum = LuaSetup.LuaUserData.CreateEnumTable
local AddCallMetaTable = LuaSetup.LuaUserData.AddCallMetaTable

local localizedStrings = {
    "LocalizedString", "LimitLString", "WrappedLString", "AddedPunctuationLString", "CapitalizeLString", "ConcatLString", "FallbackLString", "FormattedLString", "InputTypeLString", "JoinLString", "LowerLString", "RawLString", "ReplaceLString", "ServerMsgLString", "SplitLString", "TagLString", "TrimLString", "UpperLString", "StripRichTagsLString",
}

for key, value in pairs(localizedStrings) do
	defaultLib[value] = CreateStatic("Barotrauma." .. value, true)
end

local sounds = {}
sounds.LowpassFilter = CreateStatic("Barotrauma.Sounds.LowpassFilter")
sounds.HighpassFilter = CreateStatic("Barotrauma.Sounds.HighpassFilter")
sounds.BandpassFilter = CreateStatic("Barotrauma.Sounds.BandpassFilter")
sounds.NotchFilter = CreateStatic("Barotrauma.Sounds.NotchFilter")
sounds.LowShelfFilter = CreateStatic("Barotrauma.Sounds.LowShelfFilter")
sounds.HighShelfFilter = CreateStatic("Barotrauma.Sounds.HighShelfFilter")
sounds.PeakFilter = CreateStatic("Barotrauma.Sounds.PeakFilter")
defaultLib["Sounds"] = sounds

defaultLib["SpriteEffects"] = CreateStatic("Microsoft.Xna.Framework.Graphics.SpriteEffects")

defaultLib["SoundPlayer"] = CreateStatic("Barotrauma.SoundPlayer")
defaultLib["SoundPrefab"] = CreateStatic("Barotrauma.SoundPrefab", true)
defaultLib["BackgroundMusic"] = CreateStatic("Barotrauma.BackgroundMusic", true)
defaultLib["GUISound"] = CreateStatic("Barotrauma.GUISound", true)
defaultLib["DamageSound"] = CreateStatic("Barotrauma.DamageSound", true)
defaultLib["WaterRenderer"] = CreateStatic("Barotrauma.WaterRenderer", true)

defaultLib["TextureLoader"] = CreateStatic("Barotrauma.TextureLoader")
defaultLib["Sprite"] = CreateStatic("Barotrauma.Sprite", true)
defaultLib["PlayerInput"] = CreateStatic("Barotrauma.PlayerInput", true)

defaultLib["Keys"] = CreateStatic("Microsoft.Xna.Framework.Input.Keys", true)

defaultLib["GUI"] = {
    GUI = CreateStatic("Barotrauma.GUI", true),
    Style = CreateStatic("Barotrauma.GUIStyle", true),
    Component = CreateStatic("Barotrauma.GUIComponent"),
    RectTransform = CreateStatic("Barotrauma.RectTransform", true),
    LayoutGroup = CreateStatic("Barotrauma.GUILayoutGroup", true),
    Button = CreateStatic("Barotrauma.GUIButton", true),
    TextBox = CreateStatic("Barotrauma.GUITextBox", true),
    Canvas = CreateStatic("Barotrauma.GUICanvas", true),
    Frame = CreateStatic("Barotrauma.GUIFrame", true),
    TextBlock = CreateStatic("Barotrauma.GUITextBlock", true),
    TickBox = CreateStatic("Barotrauma.GUITickBox", true),
    Image = CreateStatic("Barotrauma.GUIImage", true),
    ListBox = CreateStatic("Barotrauma.GUIListBox", true),
    ScrollBar = CreateStatic("Barotrauma.GUIScrollBar", true),
    DropDown = CreateStatic("Barotrauma.GUIDropDown", true),
    NumberInput = CreateStatic("Barotrauma.GUINumberInput", true),
    MessageBox = CreateStatic("Barotrauma.GUIMessageBox", true),
    ColorPicker = CreateStatic("Barotrauma.GUIColorPicker", true),
    ProgressBar = CreateStatic("Barotrauma.GUIProgressBar", true),
    CustomComponent = CreateStatic("Barotrauma.GUICustomComponent", true),
    ScissorComponent = CreateStatic("Barotrauma.GUIScissorComponent", true),
    VideoPlayer = CreateStatic("Barotrauma.VideoPlayer", true),
    Graph = CreateStatic("Barotrauma.Graph", true),
    SerializableEntityEditor = CreateStatic("Barotrauma.SerializableEntityEditor", true),
    SlideshowPlayer = CreateStatic("Barotrauma.SlideshowPlayer", true),
    CreditsPlayer = CreateStatic("Barotrauma.CreditsPlayer", true),
    DragHandle = CreateStatic("Barotrauma.GUIDragHandle", true),
    ContextMenu = CreateStatic("Barotrauma.GUIContextMenu", true),
    ContextMenuOption = CreateStatic("Barotrauma.ContextMenuOption", true),

    Screen = CreateStatic("Barotrauma.Screen"),

    Anchor = CreateStatic("Barotrauma.Anchor"),
    Alignment = CreateStatic("Barotrauma.Alignment"),
    Pivot = CreateStatic("Barotrauma.Pivot"),
    SoundType = CreateEnum("Barotrauma.GUISoundType"),
    CursorState = CreateEnum("Barotrauma.CursorState"),

    GUIStyle = CreateStatic("Barotrauma.GUIStyle", true),
}

setmetatable(defaultLib["GUI"], {
    __index = function (table, key)
        return defaultLib["GUI"].GUI[key]
    end
})

AddCallMetaTable(defaultLib["GUI"].VideoPlayer.VideoSettings)
AddCallMetaTable(defaultLib["GUI"].VideoPlayer.TextSettings)

return defaultLib