local slotRefresher = {}
local slotLimits = require("slot_limits")

local EXTRA_SLOT_INDICES = slotLimits.extraSlotIndices()
local refreshingWidgets = {}

local function unwrap(value)
    if value == nil then return nil end
    local ok, unwrapped = pcall(function() return value:get() end)
    if ok then return unwrapped end
    return value
end

local function describe(object)
    local target = unwrap(object)
    if target == nil then return "nil" end

    local ok, fullName = pcall(function() return target:GetFullName() end)
    if ok and fullName ~= nil then return tostring(fullName) end
    return tostring(target)
end

local function readStackCount(slot)
    local ok, value = pcall(function() return slot.StackCount end)
    if not ok then return "unknown" end
    return tostring(tonumber(unwrap(value)) or unwrap(value))
end

local function refreshWidget(widget, targetSlot)
    local innerOk, innerWidget = pcall(function() return widget.MyItemSlotWidget end)
    innerWidget = innerOk and unwrap(innerWidget) or nil
    if innerWidget == nil then return false, "inner-widget-unavailable", "none" end

    local refreshOk, refreshError = pcall(function()
        local refreshFunction = innerWidget["On Update Slot Internal"]
        if refreshFunction == nil then error("inner-update-function-unavailable") end
        refreshFunction(innerWidget, targetSlot)
    end)

    local concreteOk, concreteError = pcall(function()
        local visibleSlot = unwrap(widget.WBP_PalInGameMenuItemSlot)
        if visibleSlot == nil then visibleSlot = innerWidget end

        local empty = unwrap(targetSlot:IsEmpty())
        if empty == true then
            visibleSlot:EmptySlotEvent()
            widget:OnSetEmptySlotImpl()
        else
            visibleSlot:ValidSlotEvent()
            visibleSlot:UpdateSlotEvent(targetSlot)

            local validFunction = widget["On Set Valid Slot Impl"]
            if validFunction ~= nil then validFunction(widget) end
        end
    end)
    if concreteOk then
        return true,
            refreshOk and nil or tostring(refreshError),
            "concrete-visual-event"
    end

    if refreshOk then
        return true,
            "concrete=" .. tostring(concreteError),
            "inner-on-update-slot"
    end

    local setupOk, setupError = pcall(function()
        widget:Setup(targetSlot)
    end)
    if setupOk then
        pcall(function() widget:OnUpdateSlot_Internal(targetSlot) end)
        return true, tostring(refreshError), "widget-setup"
    end

    local fallbackOk, fallbackError = pcall(function()
        widget:OnUpdateSlot_Internal(targetSlot)
    end)
    if fallbackOk then
        return true,
            tostring(refreshError) .. "; setup=" .. tostring(setupError),
            "button-on-update-slot"
    end

    return false,
        tostring(refreshError)
            .. "; concrete=" .. tostring(concreteError)
            .. "; setup=" .. tostring(setupError)
            .. "; fallback=" .. tostring(fallbackError),
        "failed"
end

function slotRefresher.refresh(ownerWidget, container, emit, reason)
    local owner = unwrap(ownerWidget)
    local targetContainer = unwrap(container)
    if owner == nil or targetContainer == nil then return false end

    local arrayOk, accessoryWidgets = pcall(function() return owner.AccessorySlots end)
    if not arrayOk
        or accessoryWidgets == nil
        or #accessoryWidgets < slotLimits.totalAccessorySlots() then
        emit("prototype.slot-refresh-skipped", {
            "reason=accessory-widgets-unavailable",
            "source=" .. tostring(reason),
        })
        return false
    end

    local refreshedCount = 0
    for extraIndex, slotIndex in ipairs(EXTRA_SLOT_INDICES) do
        local widget = unwrap(accessoryWidgets[4 + extraIndex])
        local slotOk, targetSlot = pcall(function()
            return targetContainer:Get(slotIndex)
        end)
        targetSlot = slotOk and unwrap(targetSlot) or nil
        local widgetKey = describe(widget)

        if widget ~= nil and targetSlot ~= nil and not refreshingWidgets[widgetKey] then
            refreshingWidgets[widgetKey] = true
            local refreshOk, refreshError, refreshMethod = refreshWidget(widget, targetSlot)
            refreshingWidgets[widgetKey] = nil

            if refreshOk then refreshedCount = refreshedCount + 1 end
            emit("prototype.slot-refreshed", {
                "source=" .. tostring(reason),
                "slotIndex=" .. tostring(slotIndex),
                "stackCount=" .. readStackCount(targetSlot),
                "method=" .. tostring(refreshMethod),
                "success=" .. tostring(refreshOk),
                "detail=" .. tostring(refreshError or "none"),
            })
        end
    end

    pcall(function()
        owner:InvalidateLayoutAndVolatility()
        owner:ForceLayoutPrepass()
    end)
    return refreshedCount == #EXTRA_SLOT_INDICES
end

return slotRefresher
