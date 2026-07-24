local slotExpander = {}
local equipmentStorage = require("equipment_storage")
local slotRefresher = require("slot_refresher")
local slotVisualStyle = require("slot_visual_style")
local slotLimits = require("slot_limits")

local EXTRA_SLOT_INDICES = slotLimits.extraSlotIndices()
local EXTRA_SLOT_POSITIONS = slotLimits.extraSlotPositions()
local TARGET_ACCESSORY_WIDGETS = slotLimits.totalAccessorySlots()
local retainedObjects = {}
local raisedEquipmentLayers = {}
local extraWidgetKeys = {}
local extraWidgetOwners = {}
local installedInputHooks = {}
local reboundWidgetKeys = {}
local inheritedWidgetKeys = {}
local activeDropKeys = {}
local dropForwardedKeys = {}
local INTERACTION_FLAGS = {
    "IsEnableDragDrop",
    "IsDisplayingItemInfo",
    "IsDisplayCommonItemInfoWindow",
    "IsEnableSpreadLift",
    "IsUsableSlot",
    "bIsLongPressUsableSLot",
    "IsSupportedQuickMove",
    "IsEnableQuickEquip",
    "IsShowPrice",
    "bShouldDisplayItemInfo",
}
local INTERACTION_DELEGATES = {
    "OnClickedButton",
    "OnMiddleClickedButton",
    "OnRightClickedButton",
    "OnHoveredButton",
    "OnUnhoveredButton",
    "OnFocusedWidget",
    "OnUnFocusedWidget",
    "OnDragged",
    "OnDropped",
    "OnDropCanceled",
    "OnLiftedButton",
    "OnFinishLiftedButton",
    "OnRequestUseItem",
    "OnTriedEquipSlot",
    "OnUpdateSlot",
}
local INPUT_EVENT_NAMES = {
    ["OnClicked_Internal"] = true,
    ["OnDropped_Internal"] = true,
    ["OnDragged_Internal"] = true,
    ["OnDrop"] = true,
    ["OnMouseButtonDown"] = true,
    ["OnMouseButtonUp"] = true,
    ["OnPreviewMouseButtonDown"] = true,
    ["TryEquipSlot"] = true,
    ["On Right Clicked Internal"] = true,
    ["OnHoveredEvent"] = true,
    ["On Hovered Internal"] = true,
    ["Focus"] = true,
    ["OnAddedToFocusPath"] = true,
}
local HIGHLIGHT_REAPPLY_EVENTS = {
    ["OnHoveredEvent"] = true,
    ["On Hovered Internal"] = true,
    ["Focus"] = true,
    ["OnAddedToFocusPath"] = true,
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

local function readObjectProperty(object, propertyName)
    local target = unwrap(object)
    if target == nil then return nil end
    local ok, value = pcall(function() return target[propertyName] end)
    if not ok then return nil end
    return unwrap(value)
end

local function classNameOf(object)
    local target = unwrap(object)
    if target == nil then return "nil" end
    local ok, className = pcall(function()
        return target:GetClass():GetFName():ToString()
    end)
    if ok and className ~= nil then return tostring(className) end
    return "unknown"
end

local function resolveItemSlot(candidate, depth, seen)
    local target = unwrap(candidate)
    if target == nil or depth > 4 then return nil end

    local key = describe(target)
    if seen[key] then return nil end
    seen[key] = true
    if string.find(key, "PalItemSlot ", 1, true) == 1 then return target end

    local propertyNames = {
        "TargetSlot",
        "ItemSlot",
        "SourceSlot",
        "Slot",
        "MyItemSlotWidget",
        "Payload",
        "DefaultDragVisual",
        "Widget",
    }
    for _, propertyName in ipairs(propertyNames) do
        local resolved = resolveItemSlot(
            readObjectProperty(target, propertyName),
            depth + 1,
            seen
        )
        if resolved ~= nil then return resolved end
    end
    return nil
end

local function findEquipmentDisplay(ownerWidget)
    local current = unwrap(ownerWidget)
    for _ = 1, 8 do
        if current == nil then return nil end
        if classNameOf(current) == "WBP_InventoryEquipment_ForDisplay_C" then
            return current
        end
        local outerOk, outer = pcall(function() return current:GetOuter() end)
        current = outerOk and unwrap(outer) or nil
    end
    return nil
end

local function copyProperty(target, source, propertyName)
    local readOk, value = pcall(function() return source[propertyName] end)
    if not readOk then return false end
    return pcall(function() target[propertyName] = value end)
end

local function raiseEquipmentLayer(ownerWidget, emit)
    local ownerKey = describe(ownerWidget)
    if raisedEquipmentLayers[ownerKey] then return end

    local ok, previousZOrder = pcall(function()
        local canvas = unwrap(ownerWidget.Canvas_EquipmentSlots)
        if canvas == nil then error("equipment canvas unavailable") end
        local canvasSlot = unwrap(canvas.Slot)
        if canvasSlot == nil then error("equipment canvas slot unavailable") end
        local oldValue = tonumber(unwrap(canvasSlot.ZOrder)) or 0
        canvasSlot:SetZOrder(100)
        canvas:InvalidateLayoutAndVolatility()
        return oldValue
    end)
    if ok then
        raisedEquipmentLayers[ownerKey] = true
        emit("prototype.layer-raised", {
            "owner=" .. ownerKey,
            "previousZOrder=" .. tostring(previousZOrder),
            "newZOrder=100",
        })
    else
        emit("prototype.layer-failed", {
            "owner=" .. ownerKey,
            "error=" .. tostring(previousZOrder),
        })
    end
end

local function rebindInternalButton(widget, emit, slotIndex)
    local widgetKey = describe(widget)
    if reboundWidgetKeys[widgetKey] then return end

    local initializeOk = pcall(function() widget:Initialize() end)
    local rebindOk, rebindError = pcall(function()
        local unbindFunction = widget["Unbind Button Events"]
        local bindFunction = widget["Bind Button Events"]
        if unbindFunction == nil or bindFunction == nil then
            error("internal bind functions unavailable")
        end
        unbindFunction()
        bindFunction()
    end)
    emit("prototype.internal-button-bound", {
        "slotIndex=" .. tostring(slotIndex),
        "initialized=" .. tostring(initializeOk),
        "rebound=" .. tostring(rebindOk),
        "error=" .. tostring(rebindOk and "none" or rebindError),
    })
    if initializeOk and rebindOk then reboundWidgetKeys[widgetKey] = true end
end

local function installInputHooks(widget, ownerWidget, emit)
    local installedWidgetKey = describe(widget)
    extraWidgetKeys[installedWidgetKey] = true
    extraWidgetOwners[installedWidgetKey] = unwrap(ownerWidget)

    local classOk, currentClass = pcall(function() return widget:GetClass() end)
    currentClass = classOk and unwrap(currentClass) or nil
    while currentClass ~= nil do
        pcall(function()
            currentClass:ForEachFunction(function(inputFunction)
                local functionName = tostring(inputFunction:GetFName():ToString())
                if not INPUT_EVENT_NAMES[functionName] then return end

                local functionPath = tostring(inputFunction:GetFullName())
                if installedInputHooks[functionPath] then return end

                local function inputPre(context, _, _, operation)
                    local target = unwrap(context)
                    local targetKey = describe(target)
                    if not extraWidgetKeys[targetKey] then return end

                    if functionName == "OnDrop" then
                        activeDropKeys[targetKey] = false
                        if not dropForwardedKeys[targetKey] then
                            dropForwardedKeys[targetKey] = true
                            local operationObject = unwrap(operation)
                            local payload = readObjectProperty(operationObject, "Payload")
                            local sourceSlot = resolveItemSlot(operationObject, 0, {})
                            local targetSlot = resolveItemSlot(target, 0, {})
                            local equipmentDisplay = findEquipmentDisplay(
                                extraWidgetOwners[targetKey]
                            )
                            local swapOk = false
                            local swapError = "source-or-target-unavailable"
                            if sourceSlot ~= nil
                                and targetSlot ~= nil
                                and equipmentDisplay ~= nil then
                                swapOk, swapError = pcall(function()
                                    equipmentDisplay:SwapItemSlot(sourceSlot, targetSlot)
                                end)
                            end
                            emit("prototype.drop-operation", {
                                "path=" .. functionPath,
                                "operation=" .. describe(operationObject),
                                "payload=" .. describe(payload),
                                "sourceSlot=" .. describe(sourceSlot),
                                "targetSlot=" .. describe(targetSlot),
                                "display=" .. describe(equipmentDisplay),
                                "swapSuccess=" .. tostring(swapOk),
                                "swapError=" .. tostring(swapOk and "none" or swapError),
                            })
                            local forceOk, forceError = pcall(function()
                                target:OnDropped_Internal()
                            end)
                            emit("prototype.drop-forwarded", {
                                "phase=pre",
                                "path=" .. functionPath,
                                "widget=" .. targetKey,
                                "success=" .. tostring(forceOk),
                                "error=" .. tostring(forceOk and "none" or forceError),
                            })
                            ExecuteWithDelay(250, function()
                                dropForwardedKeys[targetKey] = nil
                            end)
                        end
                    elseif functionName == "OnDropped_Internal" then
                        activeDropKeys[targetKey] = true
                    end

                    emit("prototype.input-event", {
                        "function=" .. functionName,
                        "path=" .. functionPath,
                        "widget=" .. targetKey,
                    })
                end

                local isBlueprintDrop = functionName == "OnDrop"
                    and string.find(functionPath, "WBP_PalItemSlotButtonBase", 1, true) ~= nil
                local function inputPost(context)
                    local target = unwrap(context)
                    local targetKey = describe(target)
                    if not extraWidgetKeys[targetKey] then return end

                    if HIGHLIGHT_REAPPLY_EVENTS[functionName] then
                        local targetSlot = resolveItemSlot(target, 0, {})
                        local indexOk, slotIndex = pcall(function()
                            return tonumber(unwrap(targetSlot.SlotIndex))
                        end)
                        slotVisualStyle.keepDropHighlightHidden(
                            target,
                            emit,
                            indexOk and slotIndex or "unknown",
                            functionName
                        )
                        return
                    end

                    local internalWasCalled = activeDropKeys[targetKey] == true
                    activeDropKeys[targetKey] = nil
                    if internalWasCalled then return end

                    local forceOk, forceError = pcall(function()
                        target:OnDropped_Internal()
                    end)
                    emit("prototype.drop-forwarded", {
                        "widget=" .. targetKey,
                        "success=" .. tostring(forceOk),
                        "error=" .. tostring(forceOk and "none" or forceError),
                    })
                end

                local hookOk, preId, postId
                if isBlueprintDrop or HIGHLIGHT_REAPPLY_EVENTS[functionName] then
                    hookOk, preId, postId = pcall(
                        RegisterHook,
                        functionPath,
                        inputPre,
                        inputPost
                    )
                else
                    hookOk, preId, postId = pcall(
                        RegisterHook,
                        functionPath,
                        inputPre
                    )
                end
                if hookOk and type(preId) == "number" and type(postId) == "number" then
                    installedInputHooks[functionPath] = true
                    emit("prototype.input-hook", {
                        "function=" .. functionName,
                        "path=" .. functionPath,
                    })
                end
            end)
        end)

        local superOk, superClass = pcall(function() return currentClass:GetSuperStruct() end)
        currentClass = superOk and unwrap(superClass) or nil
        if currentClass ~= nil then
            local validOk, isValid = pcall(function() return currentClass:IsValid() end)
            if not validOk or not isValid then currentClass = nil end
        end
    end
end

local function inheritInteraction(targetWidget, templateWidget, emit, slotIndex)
    local widgetKey = describe(targetWidget)
    if inheritedWidgetKeys[widgetKey] then return end

    local copiedFlags = 0
    for _, propertyName in ipairs(INTERACTION_FLAGS) do
        if copyProperty(targetWidget, templateWidget, propertyName) then
            copiedFlags = copiedFlags + 1
        end
    end

    local copiedDelegates = 0
    local copiedBindings = 0
    for _, propertyName in ipairs(INTERACTION_DELEGATES) do
        local ok, bindingCount = pcall(function()
            local sourceDelegate = templateWidget[propertyName]
            local targetDelegate = targetWidget[propertyName]
            local bindings = sourceDelegate:GetBindings()
            targetDelegate:Clear()

            local count = 0
            if bindings ~= nil then
                for _, binding in ipairs(bindings) do
                    local targetObject = unwrap(binding.Object)
                    if targetObject ~= nil then
                        targetDelegate:Add(targetObject, binding.FunctionName)
                        count = count + 1
                    end
                end
            end
            return count
        end)
        if ok then
            copiedDelegates = copiedDelegates + 1
            copiedBindings = copiedBindings + (tonumber(bindingCount) or 0)
        end
    end

    emit("prototype.interaction-inherited", {
        "slotIndex=" .. tostring(slotIndex),
        "flags=" .. tostring(copiedFlags),
        "delegates=" .. tostring(copiedDelegates),
        "bindings=" .. tostring(copiedBindings),
    })
    if copiedFlags == #INTERACTION_FLAGS and copiedDelegates == #INTERACTION_DELEGATES then
        inheritedWidgetKeys[widgetKey] = true
    end
end

local function createAccessoryWidget(ownerWidget, templateWidget, templateSizeBox, grid, emit)
    local library = unwrap(StaticFindObject("/Script/UMG.Default__WidgetBlueprintLibrary"))
    local sizeBoxClass = unwrap(StaticFindObject("/Script/UMG.SizeBox"))
    if library == nil or sizeBoxClass == nil then
        emit("prototype.ui-failed", { "reason=umg-classes-unavailable" })
        return nil, nil
    end

    local playerOk, owningPlayer = pcall(function() return ownerWidget:GetOwningPlayer() end)
    owningPlayer = playerOk and unwrap(owningPlayer) or nil
    local widgetOk, createdWidget = pcall(function()
        return library:Create(ownerWidget, templateWidget:GetClass(), owningPlayer)
    end)
    createdWidget = widgetOk and unwrap(createdWidget) or nil
    if createdWidget == nil then
        emit("prototype.ui-failed", { "reason=widget-construction-failed" })
        return nil, nil
    end

    local treeOk, widgetTree = pcall(function() return ownerWidget.WidgetTree end)
    widgetTree = treeOk and unwrap(widgetTree) or ownerWidget
    local boxOk, createdSizeBox = pcall(StaticConstructObject, sizeBoxClass, widgetTree)
    createdSizeBox = boxOk and unwrap(createdSizeBox) or nil
    if createdSizeBox == nil then
        emit("prototype.ui-failed", { "reason=size-box-construction-failed" })
        return nil, nil
    end

    local widthOk, width = pcall(function() return templateSizeBox.WidthOverride end)
    local heightOk, height = pcall(function() return templateSizeBox.HeightOverride end)
    if widthOk and tonumber(unwrap(width)) ~= nil then
        pcall(function() createdSizeBox:SetWidthOverride(tonumber(unwrap(width))) end)
    end
    if heightOk and tonumber(unwrap(height)) ~= nil then
        pcall(function() createdSizeBox:SetHeightOverride(tonumber(unwrap(height))) end)
    end

    local contentOk = pcall(function() createdSizeBox:SetContent(createdWidget) end)
    if not contentOk then
        emit("prototype.ui-failed", { "reason=size-box-content-failed" })
        return nil, nil
    end

    retainedObjects[#retainedObjects + 1] = createdWidget
    retainedObjects[#retainedObjects + 1] = createdSizeBox
    return createdWidget, createdSizeBox
end

local function visibilityOf(widget)
    local ok, value = pcall(function() return widget:GetVisibility() end)
    if not ok then return "unknown" end
    return tostring(unwrap(value))
end

local function restoreExistingWidgets(ownerWidget, accessoryWidgets, container, emit)
    local layoutRoot = nil
    local templateWidget = unwrap(accessoryWidgets[4])
    local templateParentOk, templateSizeBox = pcall(function() return templateWidget:GetParent() end)
    templateSizeBox = templateParentOk and unwrap(templateSizeBox) or nil
    local templateSlotOk, templateGridSlot = pcall(function() return templateSizeBox.Slot end)
    templateGridSlot = templateSlotOk and unwrap(templateGridSlot) or nil

    if templateWidget == nil or templateSizeBox == nil or templateGridSlot == nil then
        emit("prototype.ui-restore-failed", {
            "reason=restore-layout-template-unavailable",
        })
        return false
    end

    for extraIndex, slotIndex in ipairs(EXTRA_SLOT_INDICES) do
        local widget = unwrap(accessoryWidgets[4 + extraIndex])
        local parentOk, sizeBox = pcall(function() return widget:GetParent() end)
        sizeBox = parentOk and unwrap(sizeBox) or nil
        local gridOk, grid = pcall(function() return sizeBox:GetParent() end)
        grid = gridOk and unwrap(grid) or nil
        local targetSlotOk, targetSlot = pcall(function() return container:Get(slotIndex) end)
        targetSlot = targetSlotOk and unwrap(targetSlot) or nil

        if widget == nil or sizeBox == nil or grid == nil or targetSlot == nil then
            emit("prototype.ui-restore-failed", {
                "reason=existing-widget-state-unavailable",
                "slotIndex=" .. tostring(slotIndex),
            })
            return false
        end

        local widgetVisibilityBefore = visibilityOf(widget)
        local boxVisibilityBefore = visibilityOf(sizeBox)
        local restoreOk = pcall(function()
            local position = EXTRA_SLOT_POSITIONS[extraIndex]
            sizeBox:RemoveFromParent()
            local reattachedSlot = unwrap(grid:AddChildToGrid(
                sizeBox,
                position.row,
                position.column
            ))
            if reattachedSlot == nil then
                error("grid reattachment returned no slot")
            end

            reattachedSlot:SetPadding(templateGridSlot.Padding)
            if position.row == 0 then
                local padding = reattachedSlot.Padding
                local currentTop = tonumber(unwrap(padding.Top)) or 0
                padding.Top = currentTop - 11
                reattachedSlot:SetPadding(padding)
            end

            rebindInternalButton(widget, emit, slotIndex)
            widget:Setup(targetSlot)
            inheritInteraction(widget, templateWidget, emit, slotIndex)
            installInputHooks(widget, ownerWidget, emit)
            widget:SetSlotUnlock()
            slotVisualStyle.hideDropHighlight(widget, emit, slotIndex)
            widget:SetIsEnabled(true)
            widget:SetRenderOpacity(1.0)
            widget:SetVisibility(0)
            sizeBox:SetIsEnabled(true)
            sizeBox:SetRenderOpacity(1.0)
            sizeBox:SetVisibility(0)
            widget:InvalidateLayoutAndVolatility()
            sizeBox:InvalidateLayoutAndVolatility()
            grid:InvalidateLayoutAndVolatility()
        end)
        if not restoreOk then
            emit("prototype.ui-restore-failed", {
                "reason=existing-widget-restore-error",
                "slotIndex=" .. tostring(slotIndex),
            })
            return false
        end

        layoutRoot = grid
        emit("prototype.widget-restored", {
            "slotIndex=" .. tostring(slotIndex),
            "widgetVisibilityBefore=" .. widgetVisibilityBefore,
            "boxVisibilityBefore=" .. boxVisibilityBefore,
            "widgetVisibilityAfter=" .. visibilityOf(widget),
            "boxVisibilityAfter=" .. visibilityOf(sizeBox),
            "reattached=true",
        })
    end

    pcall(function()
        layoutRoot:ForceLayoutPrepass()
        ownerWidget:InvalidateLayoutAndVolatility()
        ownerWidget:ForceLayoutPrepass()
    end)
    return true
end

local function ensureWidgets(ownerWidget, container, emit)
    local target = unwrap(ownerWidget)
    if target == nil then return false end

    local arrayOk, accessoryWidgets = pcall(function() return target.AccessorySlots end)
    if not arrayOk or accessoryWidgets == nil then
        emit("prototype.ui-failed", { "reason=accessory-array-unavailable" })
        return false
    end
    raiseEquipmentLayer(target, emit)
    if #accessoryWidgets >= TARGET_ACCESSORY_WIDGETS then
        local restored = restoreExistingWidgets(target, accessoryWidgets, container, emit)
        emit("prototype.ui-ready", {
            "widgets=" .. tostring(#accessoryWidgets),
            "existing=true",
            "restored=" .. tostring(restored),
        })
        return restored
    end
    if #accessoryWidgets ~= 4 then
        emit("prototype.ui-failed", {
            "reason=unexpected-widget-count",
            "widgets=" .. tostring(#accessoryWidgets),
        })
        return false
    end

    local templateWidget = unwrap(accessoryWidgets[4])
    local parentOk, templateSizeBox = pcall(function() return templateWidget:GetParent() end)
    templateSizeBox = parentOk and unwrap(templateSizeBox) or nil
    local gridOk, grid = pcall(function() return templateSizeBox:GetParent() end)
    grid = gridOk and unwrap(grid) or nil
    local templateSlotOk, templateGridSlot = pcall(function() return templateSizeBox.Slot end)
    templateGridSlot = templateSlotOk and unwrap(templateGridSlot) or nil
    if templateWidget == nil or templateSizeBox == nil or grid == nil or templateGridSlot == nil then
        emit("prototype.ui-failed", { "reason=layout-template-unavailable" })
        return false
    end

    for extraIndex, slotIndex in ipairs(EXTRA_SLOT_INDICES) do
        local createdWidget, createdSizeBox = createAccessoryWidget(
            target,
            templateWidget,
            templateSizeBox,
            grid,
            emit
        )
        if createdWidget == nil then return false end

        local position = EXTRA_SLOT_POSITIONS[extraIndex]
        local row = position.row
        local column = position.column
        local addOk, createdGridSlot = pcall(function()
            return grid:AddChildToGrid(createdSizeBox, row, column)
        end)
        createdGridSlot = addOk and unwrap(createdGridSlot) or nil
        if createdGridSlot == nil then
            emit("prototype.ui-failed", {
                "reason=grid-add-failed",
                "slotIndex=" .. tostring(slotIndex),
            })
            return false
        end

        local paddingOk = pcall(function()
            createdGridSlot:SetPadding(templateGridSlot.Padding)
        end)
        if not paddingOk then
            emit("prototype.ui-failed", {
                "reason=grid-padding-failed",
                "slotIndex=" .. tostring(slotIndex),
            })
            return false
        end

        if position.row == 0 then
            local upperSpacingOk = pcall(function()
                local padding = createdGridSlot.Padding
                local currentTop = tonumber(unwrap(padding.Top)) or 0
                padding.Top = currentTop - 11
                createdGridSlot:SetPadding(padding)
            end)
            if not upperSpacingOk then
                emit("prototype.ui-failed", {
                    "reason=upper-slot-spacing-failed",
                    "slotIndex=" .. tostring(slotIndex),
                })
                return false
            end
        end

        local targetSlotOk, targetSlot = pcall(function() return container:Get(slotIndex) end)
        targetSlot = targetSlotOk and unwrap(targetSlot) or nil
        if targetSlot == nil then
            emit("prototype.ui-failed", {
                "reason=target-slot-unavailable",
                "slotIndex=" .. tostring(slotIndex),
            })
            return false
        end

        local setupOk = pcall(function()
            rebindInternalButton(createdWidget, emit, slotIndex)
            createdWidget:Setup(targetSlot)
            inheritInteraction(createdWidget, templateWidget, emit, slotIndex)
            installInputHooks(createdWidget, ownerWidget, emit)
            createdWidget:SetSlotUnlock()
            slotVisualStyle.hideDropHighlight(createdWidget, emit, slotIndex)
        end)
        if not setupOk then
            emit("prototype.ui-failed", {
                "reason=widget-setup-failed",
                "slotIndex=" .. tostring(slotIndex),
            })
            return false
        end

        accessoryWidgets[#accessoryWidgets + 1] = createdWidget
        emit("prototype.widget-created", {
            "slotIndex=" .. tostring(slotIndex),
            "row=" .. tostring(row),
            "column=" .. tostring(column),
            "paddingCopied=true",
            "upperSpacingAdjusted=" .. tostring(position.row == 0),
            "widget=" .. describe(createdWidget),
        })
    end

    emit("prototype.ui-ready", {
        "widgets=" .. tostring(#accessoryWidgets),
        "existing=false",
    })
    return true
end

function slotExpander.expandLocal(inventory, ownerWidget, emit)
    local ok, result = xpcall(function()
        local container = equipmentStorage.ensureInventory(inventory, emit, "ui-expand")
        if container == nil then return false end
        return ensureWidgets(ownerWidget, container, emit)
    end, debug.traceback)

    if not ok then
        emit("prototype.failed", { "error=" .. tostring(result) })
        return false
    end
    return result == true
end

function slotExpander.refreshLocal(inventory, ownerWidget, emit, reason)
    local ok, result = xpcall(function()
        local container = equipmentStorage.ensureInventory(inventory, emit, "ui-refresh")
        if container == nil then return false end
        return slotRefresher.refresh(ownerWidget, container, emit, reason)
    end, debug.traceback)

    if not ok then
        emit("prototype.slot-refresh-failed", { "error=" .. tostring(result) })
        return false
    end
    return result == true
end

function slotExpander.hasCompleteUi(ownerWidget)
    local target = unwrap(ownerWidget)
    if target == nil then return false end

    local arrayOk, accessoryWidgets = pcall(function() return target.AccessorySlots end)
    if not arrayOk or accessoryWidgets == nil or #accessoryWidgets < TARGET_ACCESSORY_WIDGETS then
        return false
    end

    for arrayIndex, slotIndex in ipairs(EXTRA_SLOT_INDICES) do
        local widget = unwrap(accessoryWidgets[4 + arrayIndex])
        local slotOk, targetSlot = pcall(function()
            local outSlot = nil
            widget:GetTargetSlot(outSlot)
            return outSlot
        end)
        if not slotOk or targetSlot == nil then
            local directOk, directSlot = pcall(function() return widget.MyItemSlotWidget.TargetSlot end)
            targetSlot = directOk and unwrap(directSlot) or nil
        end
        if targetSlot ~= nil then
            local indexOk, index = pcall(function() return tonumber(unwrap(targetSlot.SlotIndex)) end)
            if indexOk and index ~= nil and index ~= slotIndex then return false end
        end
    end
    return true
end

return slotExpander
