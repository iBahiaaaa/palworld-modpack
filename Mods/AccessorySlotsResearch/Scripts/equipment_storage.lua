local equipmentStorage = {}
local slotLimits = require("slot_limits")

local NATIVE_SLOT_COUNT = 9
local TARGET_SLOT_COUNT = slotLimits.targetEquipmentContainerSlots()
local ACCESSORY_TEMPLATE_INDEX = 7
local EXTRA_SLOT_INDICES = slotLimits.extraSlotIndices()
local retainedObjects = {}
local preparedContainers = {}
local failedInventories = {}

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

local function readSlotIndex(slot)
    local target = unwrap(slot)
    if target == nil then return nil end
    local ok, value = pcall(function() return target.SlotIndex end)
    if not ok then return nil end
    return tonumber(unwrap(value))
end

local function copyProperty(target, source, propertyName)
    local readOk, value = pcall(function() return source[propertyName] end)
    if not readOk then return false end
    return pcall(function() target[propertyName] = value end)
end

local function findEquipmentContainer(inventory)
    local target = unwrap(inventory)
    if target == nil then return nil end

    local helperOk, helper = pcall(function() return target.InventoryMultiHelper end)
    helper = helperOk and unwrap(helper) or nil
    if helper == nil then return nil end

    local containersOk, containers = pcall(function() return helper.Containers end)
    if not containersOk or containers == nil then return nil end

    local equipmentContainer = nil
    local iterationOk = pcall(function()
        containers:ForEach(function(_, containerValue)
            if equipmentContainer ~= nil then return end
            local container = unwrap(containerValue)
            if container == nil then return end

            local countOk, count = pcall(function() return container:Num() end)
            count = countOk and tonumber(count) or nil
            if count ~= nil and count >= NATIVE_SLOT_COUNT and count <= TARGET_SLOT_COUNT then
                local headOk, headSlot = pcall(function() return container:Get(0) end)
                local modifierOk, modifierSlot = pcall(function() return container:Get(8) end)
                headSlot = headOk and unwrap(headSlot) or nil
                modifierSlot = modifierOk and unwrap(modifierSlot) or nil
                if headSlot ~= nil and modifierSlot ~= nil then
                    equipmentContainer = container
                end
            end
        end)
    end)
    if not iterationOk then return nil end
    return equipmentContainer
end

local function notifyContainerChanged(inventory, container)
    pcall(function() container:OnRep_ItemSlotArray() end)
    pcall(function() inventory:RequestForceMarkAllDirty(true) end)
end

function equipmentStorage.isExtraSlot(slot)
    local index = readSlotIndex(slot)
    for _, extraIndex in ipairs(EXTRA_SLOT_INDICES) do
        if index == extraIndex then return true end
    end
    return false
end

function equipmentStorage.ensureInventory(inventory, emit, reason)
    local target = unwrap(inventory)
    if target == nil then return nil end

    local container = findEquipmentContainer(target)
    if container == nil then
        local inventoryKey = describe(target)
        local attempts = (failedInventories[inventoryKey] or 0) + 1
        failedInventories[inventoryKey] = attempts
        if attempts == 1 or attempts == 10 then
            emit("storage.waiting", {
                "inventory=" .. inventoryKey,
                "attempt=" .. tostring(attempts),
                "reason=" .. tostring(reason),
            })
        end
        return nil
    end

    local containerKey = describe(container)
    local countOk, count = pcall(function() return container:Num() end)
    count = countOk and tonumber(count) or nil
    if count ~= nil and count >= TARGET_SLOT_COUNT then
        if not preparedContainers[containerKey] then
            preparedContainers[containerKey] = true
            emit("storage.ready", {
                "container=" .. containerKey,
                "slots=" .. tostring(count),
                "effectiveSlots=" .. tostring(TARGET_SLOT_COUNT),
                "loaded=true",
                "reason=" .. tostring(reason),
            })
        end
        return container
    end

    if count == nil or count < NATIVE_SLOT_COUNT or count > TARGET_SLOT_COUNT then
        emit("storage.failed", {
            "container=" .. containerKey,
            "reason=unexpected-size",
            "slots=" .. tostring(count),
        })
        return nil
    end

    local templateOk, templateSlot = pcall(function()
        return container:Get(ACCESSORY_TEMPLATE_INDEX)
    end)
    templateSlot = templateOk and unwrap(templateSlot) or nil
    if templateSlot == nil then
        emit("storage.failed", {
            "container=" .. containerKey,
            "reason=accessory-template-unavailable",
        })
        return nil
    end

    local arrayOk, slotArray = pcall(function() return container.ItemSlotArray end)
    if not arrayOk or slotArray == nil then
        emit("storage.failed", {
            "container=" .. containerKey,
            "reason=slot-array-unavailable",
        })
        return nil
    end

    for slotIndex = count, TARGET_SLOT_COUNT - 1 do
        local createOk, createdSlot = pcall(
            StaticConstructObject,
            templateSlot:GetClass(),
            container
        )
        createdSlot = createOk and unwrap(createdSlot) or nil
        if createdSlot == nil then
            emit("storage.failed", {
                "container=" .. containerKey,
                "reason=slot-construction-failed",
                "slotIndex=" .. tostring(slotIndex),
            })
            return nil
        end

        local indexOk = pcall(function() createdSlot.SlotIndex = slotIndex end)
        local containerIdOk = copyProperty(createdSlot, templateSlot, "ContainerId")
        local permissionOk = copyProperty(createdSlot, templateSlot, "Permission")
        if not indexOk or not containerIdOk or not permissionOk then
            emit("storage.failed", {
                "container=" .. containerKey,
                "reason=slot-initialization-failed",
                "slotIndex=" .. tostring(slotIndex),
                "permissionCopied=" .. tostring(permissionOk),
            })
            return nil
        end

        slotArray[#slotArray + 1] = createdSlot
        retainedObjects[#retainedObjects + 1] = createdSlot
        emit("storage.slot-created", {
            "container=" .. containerKey,
            "slot=" .. describe(createdSlot),
            "slotIndex=" .. tostring(slotIndex),
            "permissionTemplate=" .. tostring(ACCESSORY_TEMPLATE_INDEX),
            "reason=" .. tostring(reason),
        })
    end

    notifyContainerChanged(target, container)
    preparedContainers[containerKey] = true
    emit("storage.ready", {
        "container=" .. containerKey,
        "slots=" .. tostring(container:Num()),
        "loaded=false",
        "reason=" .. tostring(reason),
    })
    return container
end

function equipmentStorage.start(emit, onInventory)
    local function prepare(inventory, reason)
        local ok, errorMessage = xpcall(function()
            local target = unwrap(inventory)
            if target == nil then return end
            if onInventory ~= nil then onInventory(target) end
            equipmentStorage.ensureInventory(target, emit, reason)
        end, debug.traceback)
        if not ok then
            emit("storage.callback-failed", {
                "reason=" .. tostring(reason),
                "error=" .. tostring(errorMessage),
            })
        end
    end

    local notifyOk, notifyError = pcall(
        NotifyOnNewObject,
        "/Script/Pal.PalPlayerInventoryData",
        function(inventory)
            prepare(inventory, "inventory-created")
            if type(ExecuteWithDelay) == "function" then
                ExecuteWithDelay(2000, function()
                    prepare(inventory, "inventory-created-delayed")
                end)
            end
        end
    )
    emit("storage.object-watch", {
        "installed=" .. tostring(notifyOk),
        "error=" .. tostring(notifyOk and "none" or notifyError),
    })

    local function scanAll(reason)
        local ok, inventories = pcall(FindAllOf, "PalPlayerInventoryData")
        if not ok or inventories == nil then return end
        for _, inventory in ipairs(inventories) do
            prepare(inventory, reason)
        end
    end

    if type(RegisterLoadMapPostHook) == "function" then
        RegisterLoadMapPostHook(function()
            if type(ExecuteWithDelay) == "function" then
                ExecuteWithDelay(3000, function() scanAll("map-loaded") end)
            end
        end)
    end

    if type(ExecuteWithDelay) == "function" then
        local function periodicScan()
            scanAll("periodic-scan")
            ExecuteWithDelay(3000, periodicScan)
        end
        ExecuteWithDelay(3000, periodicScan)
    end
end

return equipmentStorage
