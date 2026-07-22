local userConfig = require("config")

local slotLimits = {}

local NATIVE_ACCESSORY_SLOTS = 4
local HARD_MAX_ACCESSORY_SLOTS = 10
local EXTRA_STORAGE_START_INDEX = 9

local requested = tonumber(userConfig.requestedAccessorySlots)
    or NATIVE_ACCESSORY_SLOTS
requested = math.floor(requested)

local effective = math.max(
    NATIVE_ACCESSORY_SLOTS,
    math.min(requested, HARD_MAX_ACCESSORY_SLOTS)
)
local extraCount = effective - NATIVE_ACCESSORY_SLOTS

function slotLimits.requestedAccessorySlots()
    return requested
end

function slotLimits.totalAccessorySlots()
    return effective
end

function slotLimits.maxAccessorySlots()
    return HARD_MAX_ACCESSORY_SLOTS
end

function slotLimits.extraSlotIndices()
    local indices = {}
    for offset = 0, extraCount - 1 do
        indices[#indices + 1] = EXTRA_STORAGE_START_INDEX + offset
    end
    return indices
end

function slotLimits.extraSlotPositions()
    local positions = {}
    for offset = 0, extraCount - 1 do
        positions[#positions + 1] = {
            row = offset % 2,
            column = 2 + math.floor(offset / 2),
        }
    end
    return positions
end

function slotLimits.targetEquipmentContainerSlots()
    return EXTRA_STORAGE_START_INDEX + extraCount
end

return slotLimits
