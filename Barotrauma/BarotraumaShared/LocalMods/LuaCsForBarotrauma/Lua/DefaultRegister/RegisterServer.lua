local Register = LuaSetup.LuaUserData.RegisterType
local RegisterBarotrauma = LuaSetup.LuaUserData.RegisterTypeBarotrauma


local localizedStrings = {
    "LocalizedString", "AddedPunctuationLString", "CapitalizeLString", "ConcatLString", "FallbackLString", "FormattedLString", "InputTypeLString", "JoinLString", "LowerLString", "RawLString", "ReplaceLString", "ServerMsgLString", "SplitLString", "TagLString", "TrimLString", "UpperLString", "StripRichTagsLString",
}

for key, value in pairs(localizedStrings) do
    RegisterBarotrauma(value)
end

Register("Steamworks.SteamServer")

RegisterBarotrauma("Character+TeamChangeEventData")

RegisterBarotrauma("Networking.GameServer")

RegisterBarotrauma("Networking.ServerPeer")
RegisterBarotrauma("Networking.FileSender")
