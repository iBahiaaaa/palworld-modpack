local HoverTransfer = {}

local NORMAL_PRESS_TYPE = 0

local LEGACY_CHEST_CLASS = "WBP_IngameMenu_Chest_C"
local MANAGED_CHEST_CLASS = "WBP_IngameMenu_ChestManage_C"

local TARGET_LIST_PROPERTY = "WBP_PalItemScrollList"
local PLAYER_INVENTORY_PROPERTY = "WBP_Common_Inventory"
local PLAYER_LIST_PROPERTY = "WBP_PalPlayerInventoryScrollList"
local CURRENT_HOVER_PROPERTY = "CachedNowHoveringSlotButton"

local ITEM_SEARCH_LIST_PROPERTY = "WBP_IngameMenu_ItemSearchList"
local SEARCH_HOVER_PROPERTY = "LastHoverSlot"
local DISPENSER_MODEL_PROPERTY = "DispenserModel"

local function unwrap(value)
    if value == nil then return nil end
    local ok, raw = pcall(function() return value:get() end)
    if ok then return raw end
    return value
end

local function validObject(value)
    local object = unwrap(value)
    if object == nil then return nil end

    local ok, valid = pcall(function() return object:IsValid() end)
    if not ok or not valid then return nil end
    return object
end

local function objectAddress(value)
    local object = validObject(value)
    if object == nil then return nil end

    local ok, result = pcall(function() return object:GetAddress() end)
    if ok then return tonumber(result) end
    return nil
end

local function fullName(value)
    local object = validObject(value)
    if object == nil then return "" end

    local ok, result = pcall(function() return object:GetFullName() end)
    if ok and result ~= nil then return tostring(result) end
    return ""
end

local function visible(widget)
    local object = validObject(widget)
    if object == nil then return false end

    local ok, result = pcall(function() return object:IsVisible() end)
    return ok and result == true
end

local function propertyObject(owner, propertyName)
    local object = validObject(owner)
    if object == nil then return nil end

    local ok, result = pcall(function() return object[propertyName] end)
    if not ok then return nil end
    return validObject(result)
end

local function findVisibleInstance(className)
    local objects = FindAllOf(className)
    if objects == nil then return nil end

    for _, object in pairs(objects) do
        local candidate = validObject(object)
        if candidate ~= nil and visible(candidate) then return candidate end
    end
    return nil
end

local function currentListButton(list)
    local button = propertyObject(list, CURRENT_HOVER_PROPERTY)
    if button == nil or not visible(button) then return nil end
    return button
end

local function transferThroughList(list, direction, lastProcessedAddress, uiName)
    local activeList = validObject(list)
    if activeList == nil or not visible(activeList) then
        return false, uiName .. ":list_unavailable"
    end

    local button = currentListButton(activeList)
    if button == nil then return false, "no_hovered_slot" end

    local buttonAddress = objectAddress(button)
    if buttonAddress == nil then return false, uiName .. ":button_unavailable" end
    if buttonAddress == lastProcessedAddress then
        return false, "same_slot", buttonAddress
    end

    -- Replica o caminho Blueprint iniciado pelo clique direito do slot.
    local ok, transferError = pcall(function()
        activeList:OnRightClicked_Internal(button, NORMAL_PRESS_TYPE)
    end)
    if not ok then
        return false,
            uiName .. ":right_click_pipeline_failed:" .. tostring(transferError),
            buttonAddress
    end

    return true, direction, buttonAddress, fullName(button), uiName
end

local function tryManagedChest(lastProcessedAddress)
    local chest = findVisibleInstance(MANAGED_CHEST_CLASS)
    if chest == nil then return nil end

    local searchList = propertyObject(chest, ITEM_SEARCH_LIST_PROPERTY)
    local searchButton = propertyObject(searchList, SEARCH_HOVER_PROPERTY)
    if searchButton ~= nil and visible(searchButton) then
        local buttonAddress = objectAddress(searchButton)
        if buttonAddress == nil then return false, "managed:button_unavailable" end
        if buttonAddress == lastProcessedAddress then
            return false, "same_slot", buttonAddress
        end

        local model = propertyObject(chest, DISPENSER_MODEL_PROPERTY)
        if model == nil then
            return false, "managed:dispenser_model_unavailable", buttonAddress
        end

        local itemOk, itemAndNum = pcall(function()
            return searchButton:GetItemAndNum()
        end)
        if not itemOk or itemAndNum == nil then
            return false,
                "managed:item_data_unavailable:" .. tostring(itemAndNum),
                buttonAddress
        end

        local valuesOk, itemId, stackCount = pcall(function()
            return itemAndNum.ItemId, tonumber(itemAndNum.Num)
        end)
        if not valuesOk or itemId == nil or stackCount == nil or stackCount <= 0 then
            return false, "managed:item_data_invalid", buttonAddress
        end

        local transferOk, transferError = pcall(function()
            model:RequestMoveItemToInventory(itemId, stackCount)
        end)
        if not transferOk then
            return false,
                "managed:move_to_inventory_failed:" .. tostring(transferError),
                buttonAddress
        end

        return true,
            "basecamp_to_player",
            buttonAddress,
            fullName(searchButton),
            "managed"
    end

    local playerInventory = propertyObject(chest, PLAYER_INVENTORY_PROPERTY)
    local playerList = propertyObject(playerInventory, PLAYER_LIST_PROPERTY)
    local playerButton = currentListButton(playerList)
    if playerButton ~= nil then
        return transferThroughList(
            playerList,
            "player_to_basecamp",
            lastProcessedAddress,
            "managed"
        )
    end

    return false, "no_hovered_slot"
end


local function tryLegacyChest(lastProcessedAddress)
    local chest = findVisibleInstance(LEGACY_CHEST_CLASS)
    if chest == nil then return nil end

    local targetList = propertyObject(chest, TARGET_LIST_PROPERTY)
    local targetButton = currentListButton(targetList)
    if targetButton ~= nil then
        return transferThroughList(
            targetList,
            "container_to_player",
            lastProcessedAddress,
            "legacy"
        )
    end

    local playerInventory = propertyObject(chest, PLAYER_INVENTORY_PROPERTY)
    local playerList = propertyObject(playerInventory, PLAYER_LIST_PROPERTY)
    local playerButton = currentListButton(playerList)
    if playerButton ~= nil then
        return transferThroughList(
            playerList,
            "player_to_container",
            lastProcessedAddress,
            "legacy"
        )
    end

    return false, "no_hovered_slot"
end

function HoverTransfer.tryTransfer(lastProcessedAddress)
    local managedResult = table.pack(tryManagedChest(lastProcessedAddress))
    if managedResult[1] ~= nil then
        return table.unpack(managedResult, 1, managedResult.n)
    end

    local legacyResult = table.pack(tryLegacyChest(lastProcessedAddress))
    if legacyResult[1] ~= nil then
        return table.unpack(legacyResult, 1, legacyResult.n)
    end

    return false, "supported_chest_ui_not_found"
end


return HoverTransfer

