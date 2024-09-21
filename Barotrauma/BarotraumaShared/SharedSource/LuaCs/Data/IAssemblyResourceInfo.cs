using System;

namespace Barotrauma.LuaCs.Data;

public interface IAssemblyResourceInfo
{
    /// <summary>
    /// The friendly name of the assembly. Script files belonging to the same assembly should all have the same name.
    /// Legacy scripts will all be given the sanitized name of the Content Package they belong to.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Is this entry referring to a script file.
    /// </summary>
    public bool IsScriptFile { get; }
    /// <summary>
    /// Should this be compiled or loaded immediately or stored for On-Demand compilation.
    /// </summary>
    public bool LazyLoad { get; }
    /// <summary>
    /// Path to the file.
    /// </summary>
    public string Path { get; }
    /// <summary>
    /// All supported platforms.
    /// </summary>
    public Platform Platforms { get; }
    /// <summary>
    /// All supported run targets (client and/or server)
    /// </summary>
    public Target Targets { get; }
}

[Flags]
public enum Platform
{
    Linux=0x1, 
    OSX=0x2, 
    Windows=0x4
}
    
[Flags]
public enum Target
{
    Client=0x1, 
    Server=0x2
}
