using System;

namespace Barotrauma.LuaCs.Data;

// TODO: Finish
public interface IConfigDefinitionInfo
{
    public string Name { get; }
    public string PackageName { get; }
    public string DisplayName { get; }
    public string DisplayCategory { get; }
    public string ImageIcon { get; }
    public ConfigDataType Type { get; }
    public string DefaultValue { get; }
}

public enum ConfigDataType
{
    
}
