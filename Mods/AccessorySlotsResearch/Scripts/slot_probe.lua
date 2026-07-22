local slotProbe = {}

local function unwrap(value)
    if value == nil then return nil end
    local ok, unwrapped = pcall(function() return value:get() end)
    if ok then return unwrapped end
    return value
end

local function readNumber(object, propertyName)
    local target = unwrap(object)
    if target == nil then return nil end

    local ok, value = pcall(function() return target[propertyName] end)
    if not ok then return nil end
    return tonumber(unwrap(value))
end

local function describe(object)
    local target = unwrap(object)
    if target == nil then return "nil" end

    local ok, fullName = pcall(function() return target:GetFullName() end)
    if ok and fullName ~= nil then return tostring(fullName) end
    return tostring(target)
end

function slotProbe.describeSlot(slot)
    return {
        "slot=" .. describe(slot),
        "slotIndex=" .. tostring(readNumber(slot, "SlotIndex") or "unknown"),
        "stackCount=" .. tostring(readNumber(slot, "StackCount") or "unknown"),
    }
end

function slotProbe.inspectEquipmentContainer(inventory, emit)
    local target = unwrap(inventory)
    if target == nil then
        emit("container.inspect-failed", { "reason=inventory-nil" })
        return
    end

    local helperOk, helper = pcall(function() return target.InventoryMultiHelper end)
    helper = helperOk and unwrap(helper) or nil
    if helper == nil then
        emit("container.inspect-failed", { "reason=helper-unavailable" })
        return
    end

    local containersOk, containers = pcall(function() return helper.Containers end)
    if not containersOk or containers == nil then
        emit("container.inspect-failed", { "reason=containers-unavailable" })
        return
    end

    local inspected = 0
    local equipmentCandidates = 0
    local ok, errorMessage = pcall(function()
        containers:ForEach(function(_, containerValue)
            local container = unwrap(containerValue)
            if container == nil then return end

            inspected = inspected + 1
            local countOk, count = pcall(function() return container:Num() end)
            count = countOk and tonumber(count) or nil
            if count == 9 then
                equipmentCandidates = equipmentCandidates + 1
                emit("container.equipment-found", {
                    "container=" .. describe(container),
                    "slots=" .. tostring(count),
                })

                for index = 0, count - 1 do
                    local slotOk, slot = pcall(function() return container:Get(index) end)
                    if slotOk then
                        local fields = slotProbe.describeSlot(slot)
                        fields[#fields + 1] = "arrayIndex=" .. tostring(index)
                        emit("container.slot", fields)
                    end
                end
            end
        end)
    end)

    if not ok then
        emit("container.inspect-failed", { "reason=iteration-error", "error=" .. tostring(errorMessage) })
        return
    end

    emit("container.inspect-complete", {
        "containers=" .. tostring(inspected),
        "equipmentCandidates=" .. tostring(equipmentCandidates),
    })
end

return slotProbe
