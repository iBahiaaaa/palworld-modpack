local sessionLog = {}

local logHandle = nil
local logPath = nil

local function resolveLogPath()
    local source = debug.getinfo(1, "S").source or ""
    source = source:gsub("^@", "")
    local modDirectory = source:match(
        "^(.*)[/\\]Scripts[/\\]session_log%.lua$"
    )
    if modDirectory == nil then return nil end
    return modDirectory .. "/ItemStackExtender-session.log"
end

function sessionLog.initialize()
    logPath = resolveLogPath()
    if logPath == nil then return false end

    local ok, handle = pcall(io.open, logPath, "a")
    if not ok or handle == nil then return false end
    logHandle = handle
    return true
end

function sessionLog.append(line)
    if logHandle == nil then return false end
    return pcall(function()
        logHandle:write(
            os.date("%Y-%m-%d %H:%M:%S"),
            " | ",
            tostring(line),
            "\n"
        )
        logHandle:flush()
    end)
end

function sessionLog.path()
    return logPath
end

return sessionLog
