local config = require("config")
local hoverTransfer = require("hover_transfer")

local MOD_NAME = "HoverTransfer"
local MOD_VERSION = "0.3.0"
local HELPER_MODULE_NAME = "HoverTransferKeys"
local EXPECTED_HELPER_SUFFIX = "\\hovertransfer\\scripts\\hovertransferkeys.dll"
local TRUE_SENTINEL = {}

local lastSlotAddress = nil
local actionQueued = false
local lastTransferClock = 0
local sequence = 0
local lastSkipReason = nil

local function emit(eventName, fields)
    if not config.diagnosticLogging and eventName ~= "startup" and eventName ~= "bridge.failed" then return end

    sequence = sequence + 1
    local parts = {
        string.format("[%s]", MOD_NAME),
        string.format("seq=%04d", sequence),
        "event=" .. tostring(eventName),
    }
    if fields then
        for _, field in ipairs(fields) do parts[#parts + 1] = tostring(field) end
    end
    print(table.concat(parts, " | ") .. "\n")
end

local function loadNativeExport(moduleName, exportName)
    local packageTable = type(package) == "table" and package or nil
    if packageTable == nil or type(packageTable.searchpath) ~= "function" then
        return nil, "package.searchpath unavailable"
    end

    local searchOk, helperPath, searchError = pcall(
        packageTable.searchpath,
        moduleName,
        packageTable.cpath
    )
    if not searchOk or helperPath == nil then return nil, tostring(searchError or helperPath) end

    local normalized = helperPath:gsub("/", "\\"):lower()
    if normalized:sub(-#EXPECTED_HELPER_SUFFIX) ~= EXPECTED_HELPER_SUFFIX then
        return nil, "helper resolved outside the mod directory: " .. helperPath
    end

    local loadOk, loaderOrError, loadWhere = pcall(package.loadlib, helperPath, exportName)
    if not loadOk or type(loaderOrError) ~= "function" then
        return nil, tostring(loaderOrError or loadWhere or "export unavailable")
    end
    return loaderOrError, nil
end

local function queryBoolean(nativeFunction)
    local ok, value = pcall(nativeFunction, TRUE_SENTINEL)
    if not ok then return nil, tostring(value) end
    if rawequal(value, TRUE_SENTINEL) then return true, nil end
    if value == nil then return false, nil end
    return nil, "unexpected native result"
end

local isHDown, bridgeError = loadNativeExport(
    HELPER_MODULE_NAME,
    "hover_transfer_is_h_down"
)

if isHDown == nil then
    emit("bridge.failed", { "error=" .. tostring(bridgeError) })
    return
end

local function processHover()
    if actionQueued then return end

    local now = os.clock() * 1000
    if (now - lastTransferClock) < config.transferCooldownMs then return end

    actionQueued = true
    ExecuteInGameThread(function()
        local ok, transferred, reason, slotAddress, widgetName, uiName = xpcall(function()
            return hoverTransfer.tryTransfer(lastSlotAddress)
        end, debug.traceback)

        actionQueued = false
        if not ok then
            emit("transfer.error", { "error=" .. tostring(transferred) })
            return
        end

        if slotAddress ~= nil and slotAddress == lastSlotAddress then return end
        lastSlotAddress = slotAddress

        if transferred then
            lastTransferClock = os.clock() * 1000
            lastSkipReason = nil
            emit("transfer.sent", {
                "direction=" .. tostring(reason),
                "slot=" .. tostring(slotAddress),
                "widget=" .. tostring(widgetName),
                "ui=" .. tostring(uiName),
            })
        elseif reason ~= "no_hovered_slot" and
            reason ~= "empty_slot" and
            reason ~= lastSkipReason
        then
            lastSkipReason = reason
            emit("transfer.skipped", {
                "reason=" .. tostring(reason),
                "slot=" .. tostring(slotAddress),
            })
        end
    end)
end

LoopAsync(config.pollIntervalMs, function()
    local hDown, queryError = queryBoolean(isHDown)
    if hDown == nil then
        emit("bridge.failed", { "error=" .. tostring(queryError) })
        return true
    end

    if hDown then
        processHover()
    else
        lastSlotAddress = nil
        lastSkipReason = nil
    end
    return false
end)

emit("startup", {
    "version=" .. MOD_VERSION,
    "pollMs=" .. tostring(config.pollIntervalMs),
    "cooldownMs=" .. tostring(config.transferCooldownMs),
})
