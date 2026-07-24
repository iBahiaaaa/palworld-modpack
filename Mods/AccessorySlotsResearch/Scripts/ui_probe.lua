local uiProbe = {}

local INVENTORY_WIDGET_NAMES = {
    "WBP_InventoryEquipment_C",
    "PalUIInventoryEquipment",
}

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

local function arrayCount(array)
    if array == nil then return nil end
    local ok, count = pcall(function() return #array end)
    if ok then return tonumber(count) end
    return nil
end

local function booleanMethod(widget, methodName)
    local target = unwrap(widget)
    if target == nil then return false end
    local ok, value = pcall(function()
        return target[methodName](target)
    end)
    return ok and unwrap(value) == true
end

function uiProbe.widgetState(widget)
    return {
        rendered = booleanMethod(widget, "IsRendered"),
        inViewport = booleanMethod(widget, "IsInViewport"),
        visible = booleanMethod(widget, "IsVisible"),
    }
end

local function selectionScore(widget)
    local state = uiProbe.widgetState(widget)
    local score = 0
    if state.rendered then score = score + 10000 end
    if state.inViewport then score = score + 1000 end
    if state.visible then score = score + 100 end
    return score
end

function uiProbe.findInventoryWidget()
    for _, className in ipairs(INVENTORY_WIDGET_NAMES) do
        local allOk, widgets = pcall(FindAllOf, className)
        if allOk and type(widgets) == "table" then
            local bestWidget = nil
            local bestScore = -1
            for _, candidateValue in pairs(widgets) do
                local candidate = unwrap(candidateValue)
                if candidate ~= nil then
                    local slotsOk, slots = pcall(function() return candidate.AccessorySlots end)
                    local count = slotsOk and arrayCount(slots) or nil
                    local score = selectionScore(candidate)
                    if count ~= nil and count > 0 and score >= bestScore then
                        bestWidget = candidate
                        bestScore = score
                    end
                end
            end
            if bestWidget ~= nil then
                return bestWidget, className
            end
        end

        local ok, widget = pcall(FindFirstOf, className)
        widget = ok and unwrap(widget) or nil
        if widget ~= nil then
            local slotsOk, slots = pcall(function() return widget.AccessorySlots end)
            local count = slotsOk and arrayCount(slots) or nil
            if count ~= nil and count > 0 then return widget, className end
        end
    end
    return nil, nil
end

local function vectorText(value)
    local target = unwrap(value)
    if target == nil then return "unknown" end

    local ok, text = pcall(function()
        return string.format("%.2f,%.2f", tonumber(target.X), tonumber(target.Y))
    end)
    if ok then return text end
    return "unknown"
end

local function canvasLayout(widget)
    local target = unwrap(widget)
    if target == nil then return "unknown", "unknown", "nil" end

    local slotOk, panelSlot = pcall(function() return target.Slot end)
    panelSlot = slotOk and unwrap(panelSlot) or nil
    if panelSlot == nil then return "unknown", "unknown", "nil" end

    local positionOk, position = pcall(function() return panelSlot:GetPosition() end)
    local sizeOk, size = pcall(function() return panelSlot:GetSize() end)
    return positionOk and vectorText(position) or "unknown",
        sizeOk and vectorText(size) or "unknown",
        describe(panelSlot)
end

local function inspectHierarchy(widget, arrayIndex, emit)
    local current = unwrap(widget)
    for depth = 0, 6 do
        if current == nil then return end

        local position, size, panelSlot = canvasLayout(current)
        emit("ui.hierarchy", {
            "arrayIndex=" .. tostring(arrayIndex),
            "depth=" .. tostring(depth),
            "widget=" .. describe(current),
            "position=" .. position,
            "size=" .. size,
            "panelSlot=" .. panelSlot,
        })

        local parentOk, parent = pcall(function() return current:GetParent() end)
        current = parentOk and unwrap(parent) or nil
    end
end

function uiProbe.inspectInventoryWidget(widget, emit)
    local target = unwrap(widget)
    if target == nil then
        emit("ui.inspect-failed", { "reason=widget-nil" })
        return
    end

    local arrayOk, accessorySlots = pcall(function() return target.AccessorySlots end)
    if not arrayOk or accessorySlots == nil then
        emit("ui.inspect-failed", { "reason=accessory-array-unavailable" })
        return
    end

    local count = arrayCount(accessorySlots)
    local state = uiProbe.widgetState(target)
    emit("ui.inventory-found", {
        "widget=" .. describe(target),
        "accessoryWidgets=" .. tostring(count or "unknown"),
        "rendered=" .. tostring(state.rendered),
        "inViewport=" .. tostring(state.inViewport),
        "visible=" .. tostring(state.visible),
        "canvas=" .. describe(target.Canvas_EquipmentSlots),
    })

    local iterationOk, iterationError = pcall(function()
        accessorySlots:ForEach(function(index, slotValue)
            local slotWidget = unwrap(slotValue)
            local targetSlot = nil
            if slotWidget ~= nil then
                local targetOk, value = pcall(function()
                    local out = {}
                    slotWidget:GetTargetSlot(out)
                    return out.TargetSlot
                end)
                if targetOk then targetSlot = unwrap(value) end
            end

            local slotIndex = "unknown"
            if targetSlot ~= nil then
                local indexOk, value = pcall(function() return targetSlot.SlotIndex end)
                if indexOk then slotIndex = tostring(tonumber(unwrap(value)) or unwrap(value)) end
            end

            local position, size, panelSlot = canvasLayout(slotWidget)

            emit("ui.accessory-widget", {
                "arrayIndex=" .. tostring(index),
                "widget=" .. describe(slotWidget),
                "targetSlot=" .. describe(targetSlot),
                "slotIndex=" .. slotIndex,
                "position=" .. position,
                "size=" .. size,
                "panelSlot=" .. panelSlot,
            })
            inspectHierarchy(slotWidget, index, emit)
        end)
    end)

    if not iterationOk then
        emit("ui.inspect-failed", {
            "reason=iteration-error",
            "error=" .. tostring(iterationError),
        })
    end
end

return uiProbe
