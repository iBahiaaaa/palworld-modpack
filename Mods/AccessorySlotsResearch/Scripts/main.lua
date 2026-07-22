local MOD_NAME = "AccessorySlotsResearch"
local MOD_VERSION = "0.7.0"
local INVENTORY_WIDGET_CLASS =
    "/Game/Pal/Blueprint/UI/Inventory/WBP_InventoryEquipment.WBP_InventoryEquipment_C"

local slotProbe = require("slot_probe")
local uiProbe = require("ui_probe")
local slotExpander = require("slot_expander")
local equipmentStorage = require("equipment_storage")
local sessionLog = require("session_log")
local slotLimits = require("slot_limits")

local sessionLogReady = sessionLog.initialize()

local sequence = 0
local hookCount = 0
local failedCount = 0
local unlockProbeCount = 0
local unlockOverrideCount = 0
local inspectedInventories = {}
local uiScanPending = false
local slotRefreshPending = false
local UI_SCAN_DELAY_MS = 250
local UI_STABILIZE_DELAY_MS = 1200
local UI_SCAN_MAX_ATTEMPTS = 20
local latestInventory = nil

local function emit(eventName, fields)
    sequence = sequence + 1
    local parts = {
        string.format("[%s]", MOD_NAME),
        string.format("seq=%04d", sequence),
        "event=" .. tostring(eventName),
    }
    if fields then
        for _, field in ipairs(fields) do
            parts[#parts + 1] = tostring(field)
        end
    end
    local line = table.concat(parts, " | ")
    print(line .. "\n")
    sessionLog.append(line)
end

local function unwrap(value)
    if value == nil then return nil end
    local ok, unwrapped = pcall(function() return value:get() end)
    if ok then return unwrapped end
    return value
end

local function describe(value)
    local object = unwrap(value)
    if object == nil then return "nil" end

    local ok, fullName = pcall(function() return object:GetFullName() end)
    if ok and fullName ~= nil then return tostring(fullName) end
    return tostring(object)
end

local function scalar(value)
    local unwrapped = unwrap(value)
    local numeric = tonumber(unwrapped)
    if numeric ~= nil then return numeric end
    return tostring(unwrapped)
end

local function guarded(eventName, callback)
    return function(...)
        local args = { ... }
        local ok, result = xpcall(function()
            return callback(table.unpack(args))
        end, debug.traceback)
        if not ok then
            emit("callback.failed", {
                "hook=" .. eventName,
                "error=" .. tostring(result),
            })
            return nil
        end
        return result
    end
end

local function register(path, preCallback, postCallback)
    local ok, preId, postId
    if postCallback ~= nil then
        ok, preId, postId = pcall(RegisterHook, path, preCallback, postCallback)
    else
        ok, preId, postId = pcall(RegisterHook, path, preCallback)
    end

    if ok and type(preId) == "number" and type(postId) == "number" then
        hookCount = hookCount + 1
        emit("hook.installed", {
            "path=" .. path,
            "preId=" .. tostring(preId),
            "postId=" .. tostring(postId),
        })
        return
    end

    failedCount = failedCount + 1
    emit("hook.failed", {
        "path=" .. path,
        "error=" .. tostring(preId),
    })
end

equipmentStorage.start(emit, function(inventory)
    latestInventory = inventory
end)

local function scheduleUiScan(reason, attempt)
    if type(ExecuteInGameThreadWithDelay) ~= "function" then
        emit("ui.scan-failed", { "reason=scheduler-unavailable" })
        return
    end

    local currentAttempt = attempt or 1
    if currentAttempt == 1 then
        if uiScanPending then return end
        uiScanPending = true
    end
    local ok, errorMessage = pcall(
        ExecuteInGameThreadWithDelay,
        UI_SCAN_DELAY_MS,
        guarded("ui.scan", function()
            local widget, matchedClass = uiProbe.findInventoryWidget()
            if widget ~= nil then
                emit("ui.scan-found", {
                    "reason=" .. tostring(reason),
                    "attempt=" .. tostring(currentAttempt),
                    "class=" .. tostring(matchedClass),
                })
                uiProbe.inspectInventoryWidget(widget, emit)
                local expanded = slotExpander.expandLocal(latestInventory, widget, emit)
                uiScanPending = false

                local stabilizeOk, stabilizeError = pcall(
                    ExecuteInGameThreadWithDelay,
                    UI_STABILIZE_DELAY_MS,
                    guarded("ui.stabilize", function()
                        local stableWidget, stableClass = uiProbe.findInventoryWidget()
                        if stableWidget == nil then
                            emit("ui.stabilize-failed", {
                                "reason=widget-not-found",
                                "source=" .. tostring(reason),
                            })
                            return
                        end

                        emit("ui.stabilize-found", {
                            "source=" .. tostring(reason),
                            "class=" .. tostring(stableClass),
                            "initialExpanded=" .. tostring(expanded),
                        })
                        slotExpander.expandLocal(latestInventory, stableWidget, emit)
                    end)
                )
                if not stabilizeOk then
                    emit("ui.stabilize-failed", {
                        "reason=schedule-error",
                        "error=" .. tostring(stabilizeError),
                    })
                end
                return
            end

            if currentAttempt < UI_SCAN_MAX_ATTEMPTS then
                scheduleUiScan(reason, currentAttempt + 1)
            else
                uiScanPending = false
                emit("ui.scan-failed", {
                    "reason=widget-not-found",
                    "attempts=" .. tostring(currentAttempt),
                })
            end
        end)
    )
    if not ok then
        uiScanPending = false
        emit("ui.scan-failed", {
            "reason=schedule-error",
            "error=" .. tostring(errorMessage),
        })
    end
end

local function scheduleSlotRefresh(reason, inventory)
    if type(ExecuteInGameThreadWithDelay) ~= "function" then return end
    if slotRefreshPending then return end

    latestInventory = unwrap(inventory) or latestInventory
    slotRefreshPending = true
    local scheduleOk, scheduleError = pcall(
        ExecuteInGameThreadWithDelay,
        100,
        guarded("ui.slot-refresh", function()
            slotRefreshPending = false
            local widget = uiProbe.findInventoryWidget()
            if widget == nil then
                emit("prototype.slot-refresh-skipped", {
                    "reason=inventory-widget-not-found",
                    "source=" .. tostring(reason),
                })
                return
            end
            slotExpander.refreshLocal(latestInventory, widget, emit, reason)
        end)
    )
    if not scheduleOk then
        slotRefreshPending = false
        emit("prototype.slot-refresh-failed", {
            "reason=schedule-error",
            "error=" .. tostring(scheduleError),
        })
    end
end

register(
    "/Script/Pal.PalPlayerInventoryData:TryEquipSlot",
    guarded("TryEquipSlot.pre", function(context, slot)
        emit("equip.requested", {
            "inventory=" .. describe(context),
            "slot=" .. describe(slot),
        })
    end),
    guarded("TryEquipSlot.post", function(context, slot)
        emit("equip.completed", {
            "inventory=" .. describe(context),
            "slot=" .. describe(slot),
        })
        scheduleSlotRefresh("equip-completed", context)
    end)
)

register(
    "/Script/Pal.PalPlayerInventoryData:TryRemoveEquipment",
    guarded("TryRemoveEquipment.pre", function(context, slot)
        emit("remove.requested", {
            "inventory=" .. describe(context),
            "slot=" .. describe(slot),
        })
    end),
    guarded("TryRemoveEquipment.post", function(context, slot)
        emit("remove.completed", {
            "inventory=" .. describe(context),
            "slot=" .. describe(slot),
        })
        scheduleSlotRefresh("remove-completed", context)
    end)
)

register(
    "/Script/Pal.PalPlayerInventoryData:OnEquipSlotChanged",
    guarded("OnEquipSlotChanged.pre", function(context, slot, slotType)
        local fields = {
            "inventory=" .. describe(context),
            "slotType=" .. tostring(scalar(slotType)),
        }
        for _, field in ipairs(slotProbe.describeSlot(slot)) do
            fields[#fields + 1] = field
        end
        emit("equipment.changed", fields)

        local inventoryKey = describe(context)
        if not inspectedInventories[inventoryKey] then
            inspectedInventories[inventoryKey] = true
            slotProbe.inspectEquipmentContainer(context, emit)
        end
    end),
    guarded("OnEquipSlotChanged.post", function(context)
        scheduleSlotRefresh("equipment-changed", context)
    end)
)

register(
    "/Script/Pal.PalPlayerInventoryData:OnUpdateEquipmentSlot",
    guarded("OnUpdateEquipmentSlot.pre", function(context, slot)
        emit("equipment.updated", {
            "inventory=" .. describe(context),
            "slot=" .. describe(slot),
        })
    end)
)

register(
    "/Script/Pal.PalPlayerInventoryData:OnUpdateEquipmentSlot_ForServer",
    guarded("OnUpdateEquipmentSlot_ForServer.pre", function(context, slot)
        emit("equipment.server-updated", {
            "inventory=" .. describe(context),
            "slot=" .. describe(slot),
        })
    end)
)

register(
    "/Script/Pal.PalPlayerInventoryData:GetUnlockedAccessorySlotNum",
    guarded("GetUnlockedAccessorySlotNum.pre", function(context)
        latestInventory = unwrap(context)
        unlockProbeCount = unlockProbeCount + 1
        if unlockProbeCount <= 12 then
            emit("unlocked-count.queried", {
                "call=" .. tostring(unlockProbeCount),
                "inventory=" .. describe(context),
            })
        end
        scheduleUiScan("unlocked-count-query", 1)

        local container = equipmentStorage.ensureInventory(
            latestInventory,
            emit,
            "unlocked-count-query"
        )
        if container ~= nil then
            unlockOverrideCount = unlockOverrideCount + 1
            if unlockOverrideCount <= 12 then
                emit("unlocked-count.overridden", {
                    "call=" .. tostring(unlockOverrideCount),
                    "value=" .. tostring(slotLimits.totalAccessorySlots()),
                    "hardMax=" .. tostring(slotLimits.maxAccessorySlots()),
                    "reason=equipment-storage-ready",
                    "inventory=" .. describe(context),
                })
            end
            return slotLimits.totalAccessorySlots()
        end
    end)
)

register(
    "/Script/Pal.PalPlayerInventoryData:IsAccessorySlot",
    guarded("IsAccessorySlot.pre", function(context, slot)
        emit("accessory-slot.checked", {
            "inventory=" .. describe(context),
            "slot=" .. describe(slot),
        })
        if equipmentStorage.isExtraSlot(slot) then
            emit("accessory-slot.accepted", {
                "inventory=" .. describe(context),
                "slot=" .. describe(slot),
            })
            return true
        end
    end)
)

local inventoryWidgetProbeReady = false
local notifyOk, notifyError = pcall(NotifyOnNewObject, INVENTORY_WIDGET_CLASS, function(widget)
    ExecuteInGameThreadWithDelay(100, guarded("inventory-widget.inspect", function()
        uiProbe.inspectInventoryWidget(widget, emit)
        scheduleUiScan("inventory-widget-created", 1)
    end))
end)
if notifyOk then
    inventoryWidgetProbeReady = true
    emit("ui.probe-installed", { "class=" .. INVENTORY_WIDGET_CLASS })
else
    emit("ui.probe-failed", { "error=" .. tostring(notifyError) })
end

emit("startup", {
    "version=" .. MOD_VERSION,
    "mode=client-server-storage-test",
    "nativeAccessorySlots=4",
    "requestedAccessorySlots=" .. tostring(slotLimits.requestedAccessorySlots()),
    "effectiveAccessorySlots=" .. tostring(slotLimits.totalAccessorySlots()),
    "hardMaxAccessorySlots=" .. tostring(slotLimits.maxAccessorySlots()),
    "installed=" .. tostring(hookCount),
    "failed=" .. tostring(failedCount),
    "uiProbeReady=" .. tostring(inventoryWidgetProbeReady),
    "storageEnabled=true",
    "sessionLogReady=" .. tostring(sessionLogReady),
    "sessionLog=" .. tostring(sessionLog.path()),
})
