local descriptor = LuaUserData.RegisterType("Barotrauma.LuaCsSteam")

LuaUserData.AddMethod(descriptor, "GetWorkshopCollection", function (id, callback)
    id = tostring(id)

    Networking.RequestPostHTTP("https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/", function (result)
        local data = json.parse(result)

        if data.response.collectiondetails[1].children == nil then
            callback()
            return
        end

        local workshopItems = {}

        for key, value in pairs(data.response.collectiondetails[1].children) do
            table.insert(workshopItems, value.publishedfileid)
        end

        if callback then
            callback(workshopItems)
        end
    end,
    "collectioncount=1&publishedfileids[0]=" .. id,
    "application/x-www-form-urlencoded")
end)