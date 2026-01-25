math.lerp = function (a, b, t)
    if type(a) ~= "number" then error(string.format("bad argument #1 to 'lerp' (number expected, got %s)", type(a)), 2) end
    if type(b) ~= "number" then error(string.format("bad argument #2 to 'lerp' (number expected, got %s)", type(b)), 2) end
    if type(t) ~= "number" then error(string.format("bad argument #3 to 'lerp' (number expected, got %s)", type(t)), 2) end

    return a * (1 - t) + b * t
end

math.clamp = function (value, min, max)
    if type(value) ~= "number" then error(string.format("bad argument #1 to 'clamp' (number expected, got %s)", type(value)), 2) end
    if type(min) ~= "number" then error(string.format("bad argument #2 to 'clamp' (number expected, got %s)", type(min)), 2) end
    if type(max) ~= "number" then error(string.format("bad argument #3 to 'clamp' (number expected, got %s)", type(max)), 2) end

    return math.max(min, math.min(max, value))
end

math.round = function (value, decimals)
    if type(value) ~= "number" then error(string.format("bad argument #1 to 'round' (number expected, got %s)", type(value)), 2) end
    if type(decimals) ~= "number" then error(string.format("bad argument #2 to 'round' (number expected, got %s)", type(decimals)), 2) end

    decimals = decimals or 0
    local mult = 10 ^ decimals
    return math.floor(value * mult + 0.5) / mult
end

math.sign = function (value)
    if type(value) ~= "number" then error(string.format("bad argument #1 to 'sign' (number expected, got %s)", type(value)), 2) end

    return value >= 0 and 1 or -1
end

math.remap = function (value, inMin, inMax, outMin, outMax)
    if type(value) ~= "number" then error(string.format("bad argument #1 to 'remap' (number expected, got %s)", type(value)), 2) end
    if type(inMin) ~= "number" then error(string.format("bad argument #2 to 'remap' (number expected, got %s)", type(inMin)), 2) end
    if type(inMax) ~= "number" then error(string.format("bad argument #3 to 'remap' (number expected, got %s)", type(inMax)), 2) end
    if type(outMin) ~= "number" then error(string.format("bad argument #4 to 'remap' (number expected, got %s)", type(outMin)), 2) end
    if type(outMax) ~= "number" then error(string.format("bad argument #5 to 'remap' (number expected, got %s)", type(outMax)), 2) end

    return outMin + (outMax - outMin) * ((value - inMin) / (inMax - inMin))
end