local config = {}

local DEFAULTS = {
    enabled = true,
    maxStack = 100000,
    preserveSingleItems = true,
}

local function resolvePath()
    local source = debug.getinfo(1, "S").source or ""
    source = source:gsub("^@", "")
    local modDirectory = source:match(
        "^(.*)[/\\]Scripts[/\\]config%.lua$"
    )
    if modDirectory == nil then return nil end
    return modDirectory .. "/config.json"
end

local function readFile(path)
    if path == nil then return nil, "config-path-unavailable" end
    local ok, handle = pcall(io.open, path, "r")
    if not ok or handle == nil then
        return nil, "config-open-failed"
    end

    local readOk, content = pcall(function()
        local value = handle:read("*a")
        handle:close()
        return value
    end)
    if not readOk then return nil, "config-read-failed" end
    return content, nil
end

local function readBoolean(content, propertyName, fallback)
    local rawValue = content:match(
        '"' .. propertyName .. '"%s*:%s*(%a+)'
    )
    if rawValue == nil then return fallback end
    return rawValue:lower() == "true"
end

local function readInteger(content, propertyName, fallback)
    local rawValue = content:match(
        '"' .. propertyName .. '"%s*:%s*(%-?%d+)'
    )
    local parsed = tonumber(rawValue)
    if parsed == nil then return fallback end
    return math.floor(parsed)
end

function config.load()
    local settings = {
        enabled = DEFAULTS.enabled,
        maxStack = DEFAULTS.maxStack,
        preserveSingleItems = DEFAULTS.preserveSingleItems,
        path = resolvePath(),
        source = "defaults",
    }

    local content, readError = readFile(settings.path)
    if content == nil then
        settings.error = readError
        return settings
    end

    settings.enabled = readBoolean(
        content,
        "Enabled",
        settings.enabled
    )
    settings.maxStack = readInteger(
        content,
        "MaxStack",
        settings.maxStack
    )
    settings.preserveSingleItems = readBoolean(
        content,
        "PreserveSingleItems",
        settings.preserveSingleItems
    )
    settings.maxStack = math.max(
        2,
        math.min(settings.maxStack, 2000000000)
    )
    settings.source = "config.json"
    return settings
end

return config
