local stackOverride = {}

local GET_MAX_STACK_PATH =
    "/Script/Pal.PalStaticItemDataBase:GetMaxStackCount"
local loggedOverrides = 0
local loggedPreserved = 0
local MAX_OVERRIDE_LOGS = 30
local MAX_PRESERVED_LOGS = 10

local function unwrap(value)
    if value == nil then return nil end
    local ok, unwrapped = pcall(function() return value:get() end)
    if ok then return unwrapped end
    return value
end

local function describe(value)
    local target = unwrap(value)
    if target == nil then return "nil" end
    local ok, fullName = pcall(function() return target:GetFullName() end)
    if ok and fullName ~= nil then return tostring(fullName) end
    return tostring(target)
end

local function readOriginalMaximum(itemData)
    local target = unwrap(itemData)
    if target == nil then return nil end

    local ok, value = pcall(function()
        return unwrap(target.MaxStackCount)
    end)
    if not ok then return nil end
    return tonumber(value)
end

function stackOverride.install(register, guarded, emit, settings)
    return register(
        GET_MAX_STACK_PATH,
        guarded("GetMaxStackCount", function(itemData)
            if not settings.enabled then return nil end

            local originalMaximum = readOriginalMaximum(itemData)
            if originalMaximum == nil then
                emit("stack.skipped", {
                    "reason=original-maximum-unavailable",
                    "item=" .. describe(itemData),
                })
                return nil
            end

            if settings.preserveSingleItems
                and originalMaximum <= 1 then
                loggedPreserved = loggedPreserved + 1
                if loggedPreserved <= MAX_PRESERVED_LOGS then
                    emit("stack.preserved", {
                        "original=" .. tostring(originalMaximum),
                        "item=" .. describe(itemData),
                    })
                end
                return originalMaximum
            end

            loggedOverrides = loggedOverrides + 1
            if loggedOverrides <= MAX_OVERRIDE_LOGS then
                emit("stack.overridden", {
                    "original=" .. tostring(originalMaximum),
                    "target=" .. tostring(settings.maxStack),
                    "item=" .. describe(itemData),
                })
            end
            return settings.maxStack
        end)
    )
end

return stackOverride
