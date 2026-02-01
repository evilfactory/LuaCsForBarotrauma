LuaSetup = {}

local path = ...

package.path = {path .. "/Lua/?.lua"}

setmodulepaths(package.path)

local function AddTableToGlobal(tbl)
    for k, v in pairs(tbl) do
        _G[k] = v
    end
end

if SERVER then
    AddTableToGlobal(require("DefaultLib/LibServer"))
else
    AddTableToGlobal(require("DefaultLib/LibClient"))
end

AddTableToGlobal(require("DefaultLib/LibShared"))

AddTableToGlobal(require("CompatibilityLib"))

require("DefaultHook")

Descriptors = LuaSetup.LuaUserData

require("DefaultLib/Utils/Math")
require("DefaultLib/Utils/String")
require("DefaultLib/Utils/Util")
require("DefaultLib/Utils/SteamApi")

LuaSetup = nil