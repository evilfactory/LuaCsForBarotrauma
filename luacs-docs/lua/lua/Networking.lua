-- luacheck: ignore 111

--[[--
Class providing networking related tasks.
]]
-- @code Networking
-- @pragma nostrip

local Networking = {}

---
-- @realm server
Networking.FileSenderMaxPacketsPerUpdate = 4

---
-- @realm server
Networking.LastClientListUpdateID = 0

--- Send a GET HTTP Request, callback is called with the result string message, status code and headers. only url anda callback are optional.
-- @realm shared 
function Networking.HttpGet(url, callback, textData, contentType) end

--- Send a POST HTTP Request, callback is called with the result string message, status code and headers.
-- @realm shared 
function Networking.HttpPost(url, callback, textData, contentType) end

--- Sends a HTTP Request, callback is called with the result string message, status code and headers. If savePath is specified, the result will be saved as binary format in the specified path. only url and callback are optional.
-- @realm shared 
function Networking.HttpRequest(url, callback, data, method, contentType, headers, savePath) end

--- Creates a new net message, returns an IWriteMessage
-- @treturn IWriteMessage netMessage
-- @realm shared 
function Networking.Start(netMessageName) end

--- Sends a net message to the server.
-- @realm client
function Networking.Send(netMessage, deliveryMethod) end

--- Sends a net message to a connection, if the connection is null, then it sends the message to all connections
-- @realm server 
function Networking.Send(netMessage, connection, deliveryMethod) end

--- Adds a function to listen for lua net messages
-- @realm shared 
function Networking.Receive(netMessageName, callback) end

--- Writes again the lobby data of a client, useful for syncing submarine lists or other lobby options.
-- @realm server 
function Networking.ClientWriteLobby(client) end

--- Creates an entity event.
-- @realm shared
function Networking.CreateEntityEvent(entity, extraData) end

--- Updates the client permissions, call this after you i've changed the permissions of a client, so they are notified about it.
-- @realm server
function Networking.UpdateClientPermissions(client) end

