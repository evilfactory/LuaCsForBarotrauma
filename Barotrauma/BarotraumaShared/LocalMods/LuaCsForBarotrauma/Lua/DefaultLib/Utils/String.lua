string.startsWith = function(str, start)
    if type(str) ~= "string" then error(string.format("bad argument #1 to 'startsWith' (string expected, got %s)", type(str)), 2) end
    if type(start) ~= "string" then error(string.format("bad argument #2 to 'startsWith' (string expected, got %s)", type(start)), 2) end

    return string.sub(str, 1, string.len(start)) == start
end

string.endsWith = function(str, ending)
    if type(str) ~= "string" then error(string.format("bad argument #1 to 'endsWith' (string expected, got %s)", type(str)), 2) end
    if type(ending) ~= "string" then error(string.format("bad argument #2 to 'endsWith' (string expected, got %s)", type(ending)), 2) end

    return ending == "" or string.sub(str, -string.len(ending)) == ending
end