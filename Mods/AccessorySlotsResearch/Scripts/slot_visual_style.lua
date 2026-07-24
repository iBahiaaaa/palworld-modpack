local slotVisualStyle = {}

local HIGHLIGHT_WIDGETS = {
    "FocusBase",
    "FocusFrame",
}

local function unwrap(value)
    if value == nil then return nil end
    local ok, unwrapped = pcall(function() return value:get() end)
    if ok then return unwrapped end
    return value
end

function slotVisualStyle.hideDropHighlight(widget, emit, slotIndex)
    local hiddenCount = 0
    for _, propertyName in ipairs(HIGHLIGHT_WIDGETS) do
        local readOk, highlight = pcall(function() return widget[propertyName] end)
        highlight = readOk and unwrap(highlight) or nil
        if highlight ~= nil then
            local hideOk = pcall(function()
                highlight:SetVisibility(2)
            end)
            if hideOk then hiddenCount = hiddenCount + 1 end
        end
    end

    emit("prototype.drop-highlight-hidden", {
        "slotIndex=" .. tostring(slotIndex),
        "layers=" .. tostring(hiddenCount),
    })
    return hiddenCount == #HIGHLIGHT_WIDGETS
end

function slotVisualStyle.keepDropHighlightHidden(widget, emit, slotIndex, reason)
    slotVisualStyle.hideDropHighlight(widget, emit, slotIndex)
    if type(ExecuteInGameThreadWithDelay) ~= "function" then return end

    pcall(ExecuteInGameThreadWithDelay, 80, function()
        local hidden = slotVisualStyle.hideDropHighlight(widget, emit, slotIndex)
        emit("prototype.drop-highlight-reapplied", {
            "slotIndex=" .. tostring(slotIndex),
            "source=" .. tostring(reason),
            "success=" .. tostring(hidden),
        })
    end)
end

return slotVisualStyle
