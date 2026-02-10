using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace Barotrauma.LuaCs.Data;


public interface IBaseResourceInfo : IResourceInfo, IDataInfo, IDependencyInfo {}

public interface IConfigResourceInfo : IBaseResourceInfo {}

/// <summary>
/// Represents loadable Lua files.
/// </summary>
public interface ILuaScriptResourceInfo : IBaseResourceInfo
{
    /// <summary>
    /// Should this script be run automatically.
    /// </summary>
    [XmlAttribute("IsAutorun")]
    public bool IsAutorun { get; }
}

public interface IAssemblyResourceInfo : IBaseResourceInfo
{
    /// <summary>
    /// The friendly name of the assembly. Script files belonging to the same assembly should all have the same name.
    /// Legacy scripts will all be given the sanitized name of the Content Package they belong to.
    /// </summary>
    [XmlAttribute("FriendlyName")]
    public string FriendlyName { get; }
    /// <summary>
    /// Is this entry referring to a script file collection.
    /// </summary>
    [XmlAttribute("IsScript")]
    public bool IsScript { get; }
    
    /// <summary>
    /// <b>[Required(IsScript: true)] Whether the internal compiled assembly name should be named to enabled use of the
    /// <see cref="InternalsVisibleToAttribute"/> attribute.</b>
    /// </summary>
    [XmlAttribute("UseInternalAccessName")]
    public bool UseInternalAccessName { get; }
    
    /// <summary>
    /// Should the following resources only be used for Compilation MetadataReference.
    /// NOTE: Affects the entire package's assembly resources, meant for internal use only.
    /// </summary>
    [XmlAttribute("IsReferenceModeOnly")]
    public bool IsReferenceModeOnly { get; }
}


#region Collections

public interface IAssembliesResourcesInfo
{
    ImmutableArray<IAssemblyResourceInfo> Assemblies { get; }
}

public interface ILuaScriptsResourcesInfo
{
    ImmutableArray<ILuaScriptResourceInfo> LuaScripts { get; }
}

public interface IConfigsResourcesInfo
{
    ImmutableArray<IConfigResourceInfo> Configs { get; }
}

#endregion
