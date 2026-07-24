local runtimeEnvironment = {}

local function sourcePath()
    local source = debug.getinfo(1, "S").source or ""
    return source:gsub("^@", ""):gsub("\\", "/"):lower()
end

function runtimeEnvironment.isDedicatedServer()
    local path = sourcePath()
    return path:find("/palserver/", 1, true) ~= nil
        or path:find("/palserverteste/", 1, true) ~= nil
end

function runtimeEnvironment.name()
    return runtimeEnvironment.isDedicatedServer()
        and "dedicated-server"
        or "client"
end

return runtimeEnvironment
