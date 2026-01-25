Util = {}

local itemDictionary = {}
local itemGroups = {}

local function AddItem(item)
    for _, itemGroup in pairs(itemGroups) do
        if itemGroup.func(item) then
            table.insert(itemGroup.items, item)
        end
    end

    local id = item.Prefab.Identifier.Value
    if itemDictionary[id] == nil then
        itemDictionary[id] = {}
    end

    table.insert(itemDictionary[id], item)
end

Hook.Add("item.created", "luaSetup.util.itemDictionary", function (item)
    AddItem(item)
end)

Hook.Add("roundEnd", "luaSetup.util.itemDictionary", function (item)
    itemDictionary = {}
    for _, itemGroup in pairs(itemGroups) do
        itemGroup.items = {}
    end
end)

for _, item in pairs(Item.ItemList) do
    AddItem(item)
end

Util.RegisterItemGroup = function(groupName, func)
    if type(groupName) ~= "string" then
        error(string.format("bad argument #1 to 'RegisterItemGroup' (string expected, got %s)", type(groupName)), 2)
    end

    if type(func) ~= "function" then
        error(string.format("bad argument #2 to 'RegisterItemGroup' (function expected, got %s)", type(func)), 2)
    end

    local items = {}
    for _, item in pairs(Item.ItemList) do
        if func(item) then
            table.insert(items, item)
        end
    end

    itemGroups[groupName] = {
        func = func,
        items = items
    }
end

Util.GetItemGroup = function(groupName)
    if type(groupName) ~= "string" then
        error(string.format("bad argument #1 to 'GetItemGroup' (string expected, got %s)", type(groupName)), 2)
    end

    if not itemGroups[groupName] then
        error("bad argument #1 to 'GetItemGroup' couldn't find the specified groupName", 2)
    end

    return itemGroups[groupName].items or {}
end

Util.GetItemsById = function(id)
    if id == nil then
        error(string.format("bad argument #1 to 'GetItemsById' (string expected, got %s)", type(id)), 2)
    end

    return itemDictionary[id]
end

Util.FindClientCharacter = function(character)
    if CLIENT and Game.IsSingleplayer then return nil end

    for _, client in pairs(Client.ClientList) do
        if client.Character == character then
            return client
        end
    end
end