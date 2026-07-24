local staticDataSync = {}

local ITEM_DATA_CLASS_NAME = "PalStaticItemDataBase"
local ITEM_DATA_CLASS_PATH = "/Script/Pal.PalStaticItemDataBase"
local INITIAL_SCAN_DELAYS_MS = { 1000, 5000, 15000 }
local MAX_ITEM_LOGS = 40

local loggedItems = 0
local loggedFailures = 0

local function unwrap(value)
    if value == nil then return nil end
    local ok, unwrapped = pcall(function() return value:get() end)
    if ok then return unwrapped end
    return value
end

local function describe(value)
    local target = unwrap(value)
    if target == nil then return "nil" end

    local ok, fullName = pcall(function()
        return target:GetFullName()
    end)
    if ok and fullName ~= nil then return tostring(fullName) end
    return tostring(target)
end

local function readMaximum(itemData)
    local target = unwrap(itemData)
    if target == nil then return nil end

    local ok, value = pcall(function()
        return unwrap(target.MaxStackCount)
    end)
    if not ok then return nil end
    return tonumber(value)
end

local function synchronizeItem(itemData, reason, emit, settings)
    local target = unwrap(itemData)
    local currentMaximum = readMaximum(target)
    if target == nil or currentMaximum == nil then
        loggedFailures = loggedFailures + 1
        if loggedFailures <= 10 then
            emit("data.sync-skipped", {
                "reason=maximum-unavailable",
                "source=" .. tostring(reason),
                "item=" .. describe(target),
            })
        end
        return "failed"
    end

    if settings.preserveSingleItems and currentMaximum <= 1 then
        return "preserved"
    end

    if currentMaximum == settings.maxStack then
        return "unchanged"
    end

    local writeOk, writeError = pcall(function()
        target.MaxStackCount = settings.maxStack
    end)
    if not writeOk then
        loggedFailures = loggedFailures + 1
        if loggedFailures <= 10 then
            emit("data.sync-failed", {
                "source=" .. tostring(reason),
                "original=" .. tostring(currentMaximum),
                "target=" .. tostring(settings.maxStack),
                "error=" .. tostring(writeError),
                "item=" .. describe(target),
            })
        end
        return "failed"
    end

    local appliedMaximum = readMaximum(target)
    if appliedMaximum ~= settings.maxStack then
        loggedFailures = loggedFailures + 1
        if loggedFailures <= 10 then
            emit("data.sync-failed", {
                "source=" .. tostring(reason),
                "reason=verification-failed",
                "original=" .. tostring(currentMaximum),
                "expected=" .. tostring(settings.maxStack),
                "actual=" .. tostring(appliedMaximum),
                "item=" .. describe(target),
            })
        end
        return "failed"
    end

    loggedItems = loggedItems + 1
    if loggedItems <= MAX_ITEM_LOGS then
        emit("data.synchronized", {
            "source=" .. tostring(reason),
            "original=" .. tostring(currentMaximum),
            "target=" .. tostring(appliedMaximum),
            "item=" .. describe(target),
        })
    end
    return "updated"
end

local function synchronizeAll(reason, emit, settings)
    local ok, itemDataObjects = pcall(
        FindAllOf,
        ITEM_DATA_CLASS_NAME
    )
    if not ok or itemDataObjects == nil then
        emit("data.scan-failed", {
            "source=" .. tostring(reason),
            "error=" .. tostring(itemDataObjects),
        })
        return
    end

    local counters = {
        updated = 0,
        preserved = 0,
        unchanged = 0,
        failed = 0,
    }

    for _, itemData in ipairs(itemDataObjects) do
        local status = synchronizeItem(
            itemData,
            reason,
            emit,
            settings
        )
        counters[status] = (counters[status] or 0) + 1
    end

    emit("data.scan-complete", {
        "source=" .. tostring(reason),
        "objects=" .. tostring(#itemDataObjects),
        "updated=" .. tostring(counters.updated),
        "preserved=" .. tostring(counters.preserved),
        "unchanged=" .. tostring(counters.unchanged),
        "failed=" .. tostring(counters.failed),
    })
end

local function scheduleScan(delayMs, reason, emit, settings)
    ExecuteWithDelay(delayMs, function()
        local ok, errorMessage = xpcall(function()
            synchronizeAll(reason, emit, settings)
        end, debug.traceback)
        if not ok then
            emit("data.scan-callback-failed", {
                "source=" .. tostring(reason),
                "error=" .. tostring(errorMessage),
            })
        end
    end)
end

local function scheduleItemSync(itemData, delayMs, reason, emit, settings)
    ExecuteWithDelay(delayMs, function()
        local ok, errorMessage = xpcall(function()
            synchronizeItem(itemData, reason, emit, settings)
        end, debug.traceback)
        if not ok then
            emit("data.item-callback-failed", {
                "source=" .. tostring(reason),
                "error=" .. tostring(errorMessage),
                "item=" .. describe(itemData),
            })
        end
    end)
end

function staticDataSync.install(guarded, emit, settings)
    local notifyInstalled = false
    local notifyOk, notifyError = pcall(function()
        NotifyOnNewObject(
            ITEM_DATA_CLASS_PATH,
            guarded("StaticItemDataCreated", function(itemData)
                scheduleItemSync(
                    itemData,
                    250,
                    "new-item-data",
                    emit,
                    settings
                )
            end)
        )
    end)
    if notifyOk then
        notifyInstalled = true
    else
        emit("data.notify-install-failed", {
            "error=" .. tostring(notifyError),
        })
    end

    for index, delayMs in ipairs(INITIAL_SCAN_DELAYS_MS) do
        scheduleScan(
            delayMs,
            "initial-" .. tostring(index),
            emit,
            settings
        )
    end

    local loadMapInstalled = false
    local loadMapOk, loadMapError = pcall(function()
        RegisterLoadMapPostHook(function()
            scheduleScan(2000, "map-loaded", emit, settings)
        end)
    end)
    if loadMapOk then
        loadMapInstalled = true
    else
        emit("data.map-hook-install-failed", {
            "error=" .. tostring(loadMapError),
        })
    end

    emit("data.sync-installed", {
        "notify=" .. tostring(notifyInstalled),
        "loadMapHook=" .. tostring(loadMapInstalled),
        "initialScans=" .. tostring(#INITIAL_SCAN_DELAYS_MS),
    })
    return notifyInstalled or loadMapInstalled
end

return staticDataSync
