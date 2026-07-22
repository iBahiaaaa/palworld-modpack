local Policy = {}

function Policy.isActiveManualInteraction(snapshot)
    return snapshot ~= nil and
        snapshot.address ~= nil and
        snapshot.interacting == true and
        snapshot.action == 1
end

function Policy.isAcceptedCandidate(snapshot, requestedToggle)
    return Policy.isActiveManualInteraction(snapshot) and
        snapshot.toggle == requestedToggle
end

function Policy.classifyEnd(options)
    if options.recentFocusLoss == true then
        return "focus-loss"
    end
    if options.foreground == true and options.tabDown == true then
        return "focused-inventory"
    end
    if options.forceHoldMode == true and
        options.holdMode == true and
        options.holdReleaseSuppressed ~= true
    then
        return "hold-release"
    end
    return nil
end

function Policy.shouldScheduleInventoryRestore(options)
    return options.enabled == true and
        options.originalCanOpen == true and
        options.foreground == true and
        options.pendingReturnGeneration == nil and
        options.restorePending ~= true and
        options.restoreCooldown ~= true
end

return Policy
