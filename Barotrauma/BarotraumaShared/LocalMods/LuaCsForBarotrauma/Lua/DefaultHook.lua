Hook.Patch("Barotrauma.Item", "TryInteract",
{
    "Barotrauma.Character",
    "System.Boolean",
    "System.Boolean",
    "System.Boolean"
},
function(instance, p)
    if Hook.Call("item.interact", instance, p["user"], p["ignoreRequiredItems"], p["forceSelectKey"], p["forceUseKey"]) == true then
        p.PreventExecution = true
        return false
    end
end, Hook.HookMethodType.Before)

Hook.Patch("Barotrauma.Item", "ApplyTreatment",
{
    "Barotrauma.Character",
    "Barotrauma.Character",
    "Barotrauma.Limb"
},
function(instance, p)
    if Hook.Call("item.applyTreatment", instance, p["user"], p["character"], p["targetLimb"]) then
        p.PreventExecution = true
        return false
    end
end, Hook.HookMethodType.Before)

Hook.Patch("Barotrauma.Item", "Combine",
{
    "Barotrauma.Item",
    "Barotrauma.Character"
},
function(instance, p)
    if Hook.Call("item.combine", instance, p["item"], p["user"]) == true then
        p.PreventExecution = true
        return false
    end
end, Hook.HookMethodType.Before)

Hook.Patch("Barotrauma.Item", "Drop",
function(instance, p)
    if Hook.Call("item.drop", instance, p["dropper"]) == true then
        p.PreventExecution = true
        return false
    end
end, Hook.HookMethodType.Before)

Hook.Patch("Barotrauma.Item", "Equip",
{
    "Barotrauma.Character"
},
function(instance, p)
    if Hook.Call("item.equip", instance, p["character"]) == true then
        p.PreventExecution = true
        return false
    end
end, Hook.HookMethodType.Before)

Hook.Patch("Barotrauma.Item", "Unequip",
{
    "Barotrauma.Character"
},
function(instance, p)
    if Hook.Call("item.unequip", instance, p["character"]) == true then
        p.PreventExecution = true
        return false
    end
end, Hook.HookMethodType.Before)