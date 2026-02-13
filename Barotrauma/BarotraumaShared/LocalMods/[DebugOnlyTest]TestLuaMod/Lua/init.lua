print("Hello!")

Hook.Add("character.created", "test", function(character) 
    print("character.created: ", character)
end)

Hook.Add("character.death", "test", function(character) 
    print("character.death: ", character)
end)

Hook.Add("character.giveJobItems", "test", function(character) 
    print("character.giveJobItems: ", character)
end)

Hook.Add("roundStart", "test", function()
    print("roundStart")
end)

Hook.Add("roundEnd", "test", function()
    print("roundEnd")
end)

Hook.Add("missionsEnded", "test", function()
    print("missionsEnded")
end)