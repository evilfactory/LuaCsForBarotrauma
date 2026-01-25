if not CSActive then
    LuaUserDataIUUD = LuaUserData.RegisterType("Barotrauma.LuaSafeUserData")
    LuaUserData = LuaUserData.CreateStatic("Barotrauma.LuaSafeUserData");

    for k, v in pairs(debug) do
        if k ~= "getmetatable" and k ~= "setmetatable" and k ~= "traceback" then
            debug[k] = nil
        end
    end
end

Descriptors = LuaUserData.__new()
LuaUserDataIUUD = nil