LuaSetup = {}

local path = table.pack(...)[1]

package.path = {path .. "/?.lua"}

setmodulepaths(package.path)

-- Setup Libraries
LuaSetup.LuaUserData = LuaUserData

require("DefaultRegister/RegisterShared")

if SERVER then
    require("DefaultRegister/RegisterServer")
else
    require("DefaultRegister/RegisterClient")
end

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

require("DefaultLib/Utils/Math")
require("DefaultLib/Utils/String")
require("DefaultLib/Utils/Util")
require("DefaultLib/Utils/SteamApi")

require("PostSetup")

LuaSetup = nil

require("ModLoader")