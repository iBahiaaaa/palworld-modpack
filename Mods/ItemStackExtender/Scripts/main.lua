local MOD_NAME = "ItemStackExtender"
local MOD_VERSION = "0.2.0"

local config = require("config")
local runtimeEnvironment = require("runtime_environment")
local sessionLog = require("session_log")
local stackOverride = require("stack_override")
local staticDataSync = require("static_data_sync")

local settings = config.load()
local sequence = 0
local installedPaths = {}
local sessionLogReady = sessionLog.initialize()

local function emit(eventName, fields)
    sequence = sequence + 1
    local parts = {
        string.format("[%s]", MOD_NAME),
        string.format("seq=%04d", sequence),
        "event=" .. tostring(eventName),
    }
    if fields ~= nil then
        for _, field in ipairs(fields) do
            parts[#parts + 1] = tostring(field)
        end
    end
    local line = table.concat(parts, " | ")
    print(line .. "\n")
    sessionLog.append(line)
end

local function guarded(eventName, callback)
    return function(...)
        local args = { ... }
        local ok, result = xpcall(function()
            return callback(table.unpack(args))
        end, debug.traceback)
        if not ok then
            emit("callback.failed", {
                "hook=" .. tostring(eventName),
                "error=" .. tostring(result),
            })
            return nil
        end
        return result
    end
end

local function register(path, callback)
    if installedPaths[path] then return true end

    local findOk, functionObject = pcall(StaticFindObject, path)
    if not findOk or functionObject == nil then
        emit("hook.unavailable", {
            "path=" .. tostring(path),
        })
        return false
    end

    local ok, preId, postId = pcall(RegisterHook, path, callback)
    if ok and type(preId) == "number" and type(postId) == "number" then
        installedPaths[path] = true
        emit("hook.installed", {
            "path=" .. tostring(path),
            "preId=" .. tostring(preId),
            "postId=" .. tostring(postId),
        })
        return true
    end

    emit("hook.failed", {
        "path=" .. tostring(path),
        "error=" .. tostring(preId),
    })
    return false
end

local hookInstalled = false
local dataSyncInstalled = false
if settings.enabled then
    hookInstalled = stackOverride.install(
        register,
        guarded,
        emit,
        settings
    )
    dataSyncInstalled = staticDataSync.install(
        guarded,
        emit,
        settings
    )
end

emit("startup", {
    "version=" .. MOD_VERSION,
    "runtime=" .. runtimeEnvironment.name(),
    "enabled=" .. tostring(settings.enabled),
    "maxStack=" .. tostring(settings.maxStack),
    "preserveSingleItems=" .. tostring(
        settings.preserveSingleItems
    ),
    "configSource=" .. tostring(settings.source),
    "configPath=" .. tostring(settings.path),
    "configError=" .. tostring(settings.error or "none"),
    "hookInstalled=" .. tostring(hookInstalled),
    "dataSyncInstalled=" .. tostring(dataSyncInstalled),
    "sessionLogReady=" .. tostring(sessionLogReady),
    "sessionLog=" .. tostring(sessionLog.path()),
})
