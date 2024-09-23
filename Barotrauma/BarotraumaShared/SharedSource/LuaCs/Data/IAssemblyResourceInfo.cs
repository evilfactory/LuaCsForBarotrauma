using System;

namespace Barotrauma.LuaCs.Data;

public interface IAssemblyResourceInfo : IResourceInfo
{
    /// <summary>
    /// The friendly name of the assembly. Script files belonging to the same assembly should all have the same name.
    /// Legacy scripts will all be given the sanitized name of the Content Package they belong to.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Is this entry referring to a script file collection.
    /// </summary>
    public bool IsScript { get; }
    /// <summary>
    /// Should this be compiled or loaded immediately or stored for On-Demand compilation.
    /// </summary>
    public bool LazyLoad { get; }
}
