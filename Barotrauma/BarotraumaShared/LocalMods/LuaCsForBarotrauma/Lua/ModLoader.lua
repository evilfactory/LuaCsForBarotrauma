local LUA_MOD_REQUIRE_PATH       = "/Lua/?.lua"
local LUA_MOD_AUTORUN_PATH       = "/Lua/Autorun"
local LUA_MOD_FORCEDAUTORUN_PATH = "/Lua/ForcedAutorun"

local function EndsWith(str, suffix)
    return str:sub(-string.len(suffix)) == suffix
end

local function GetFileName(file)
    return file:match("^.+/(.+)$")
end

local function ExecuteProtected(s, folder)
    loadfile(s)(folder)
end

local function RunFolder(folder, rootFolder, package)
    local search = File.DirSearch(folder)
    for i = 1, #search, 1 do
        local s = search[i]:gsub("\\", "/")

        if EndsWith(s, ".lua") then
            local time = os.clock()
            local ok, result = pcall(ExecuteProtected, s, rootFolder)
            local diff = os.clock() - time

            print(string.format(" - %s (Took %.5fms)", GetFileName(s), diff))
            if not ok then
                printerror(result)
            end
        end

    end
end

local function AssertTypes(expectedTypes, ...)
    local args = table.pack(...)
    assert(
        #args == #expectedTypes,
        string.format(
            "Assertion failed: incorrect number of args\n\texpected = %s\n\tgot = %s",
            #expectedTypes, #args
        )
    )
    for i = 1, #args do
        local arg = args[i]
        local expectedType = expectedTypes[i]
        assert(
            type(arg) == expectedType,
            string.format(
                "Assertion failed: incorrect argument type (arg #%d)\n\texpected = %s\n\tgot = %s",
                i, expectedType, type(arg)
            )
        )
    end
end

local function ExecutionQueue()
    local executionQueue = {}
    executionQueue.Queue = {}

    executionQueue.Process = function()
        while executionQueue.Queue[1] ~= nil do
            local folder, rootFolder, package = table.unpack(table.remove(executionQueue.Queue, 1))
            print(string.format("%s %s", package.Name, package.ModVersion))
            RunFolder(folder, rootFolder, package)
        end
    end

    executionQueue.Add = function(...)
        AssertTypes({ 'string', 'string', 'userdata' }, ...)
        table.insert(executionQueue.Queue, table.pack(...))
    end

    return executionQueue
end

local QueueAutorun       = ExecutionQueue()
local QueueForcedAutorun = ExecutionQueue()

local function nocase(s)
    s = string.gsub(s, "%a", function(c)
        return string.format("[%s%s]", string.lower(c), string.upper(c))
    end)
    return s
end

local function ProcessPackages(packages, fn)
    for pkg in packages do
        if pkg then
            local pkgPath = pkg.Path
                :gsub("\\", "/")
                :gsub(nocase("/filelist.xml"), "")
            fn(pkg, pkgPath)
        end
    end
end

ProcessPackages(ContentPackageManager.EnabledPackages.All, function(pkg, pkgPath)
    table.insert(package.path, pkgPath .. LUA_MOD_REQUIRE_PATH)
    local autorunPath = pkgPath .. LUA_MOD_AUTORUN_PATH
    if File.DirectoryExists(autorunPath) then
        QueueAutorun.Add(autorunPath, pkgPath, pkg)
    end
end)

-- we don't want to execute workshop ForcedAutorun if we have a local Package
local executedLocalPackages = {}

ProcessPackages(ContentPackageManager.EnabledPackages.All, function(pkg, pkgPath)
    table.insert(package.path, pkgPath .. LUA_MOD_REQUIRE_PATH)
    local forcedAutorunPath = pkgPath .. LUA_MOD_FORCEDAUTORUN_PATH
    if File.DirectoryExists(forcedAutorunPath) then
        QueueForcedAutorun.Add(forcedAutorunPath, pkgPath, pkg)
        executedLocalPackages[pkg.Name] = true
    end
end)

if not LuaCsConfig.TreatForcedModsAsNormal then
    ProcessPackages(ContentPackageManager.LocalPackages, function(pkg, pkgPath)
        if not executedLocalPackages[pkg.Name] then
            table.insert(package.path, pkgPath .. LUA_MOD_REQUIRE_PATH)
            local forcedAutorunPath = pkgPath .. LUA_MOD_FORCEDAUTORUN_PATH
            if File.DirectoryExists(forcedAutorunPath) then
                QueueForcedAutorun.Add(forcedAutorunPath, pkgPath, pkg)
                executedLocalPackages[pkg.Name] = true
            end
        end
    end)

    ProcessPackages(ContentPackageManager.AllPackages, function(pkg, pkgPath)
        if not executedLocalPackages[pkg.Name] then
            table.insert(package.path, pkgPath .. LUA_MOD_REQUIRE_PATH)
            local forcedAutorunPath = pkgPath .. LUA_MOD_FORCEDAUTORUN_PATH
            if File.DirectoryExists(forcedAutorunPath) then
                QueueForcedAutorun.Add(forcedAutorunPath, pkgPath, pkg)
            end
        end
    end)
end

setmodulepaths(package.path)
setmodulepaths = nil

local allExecuted = {}
for key, value in pairs(QueueAutorun.Queue) do table.insert(allExecuted, value[3]) end
for key, value in pairs(QueueForcedAutorun.Queue) do table.insert(allExecuted, value[3]) end

if SERVER then
    Networking.Receive("_luastart", function (message, client)
        local num = message.ReadUInt16()

        local packages = {}

        for i = 1, num, 1 do
            table.insert(packages, {
                Name = message.ReadString(),
                Version = message.ReadString(),
                Id = message.ReadUInt64(),
                Hash = message.ReadString()
            })
        end

        Hook.Call("client.packages", client, packages)
    end)
elseif Game.IsMultiplayer then
    local message = Networking.Start("_luastart")

    message.WriteUInt16(#allExecuted)

    for key, package in pairs(allExecuted) do
        local id = package.UgcId
        local hash = package.Hash and package.Hash.StringRepresentation or ""

        if id == nil then id = 0 end

        message.WriteString(package.Name)
        message.WriteString(package.ModVersion)
        message.WriteUInt64(UInt64(id))
        message.WriteString(hash)
    end

    Networking.Send(message)
end

QueueAutorun.Process()
QueueForcedAutorun.Process()

Hook.Add("stop", "luaSetup.stop", function()
    print("Stopping Lua...")
end)

Hook.Call("loaded")