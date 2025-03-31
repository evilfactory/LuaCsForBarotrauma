using System;
using System.Xml.Linq;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Data;

/// <summary>
/// Parsed data from a configuration xml.
/// </summary>
public partial interface IConfigInfo : IDataInfo
{
    /// <summary>
    /// Specifies the type initializer that will be used to instantiate the config var.
    /// </summary>
    Type DataType { get; }
    /// <summary>
    /// String version of the default value.
    /// </summary>
    string DefaultValue { get; }
    /// <summary>
    /// The value the last time this config was saved.
    /// <br/><b>[If(Type='<see cref="string"/>')]</b><br/>
    /// The value is from the 'Value' Attribute. Typically used for types with single/simple values, such as primitives.
    /// <br/><b>[If(Type='<see cref="XElement"/>')]</b><br/>
    /// The value is from the first 'Value' child element. Typically used with complex config types, such as range and list.
    /// </summary>
    OneOf.OneOf<string, XElement> Value { get; }
    /// <summary>
    /// In what <see cref="RunState"/>(s) is this config editable. Will be editable in the selected state, and lower value states.
    /// <br/><br/>
    /// <b>[Important]</b><br/> Setting this to value lower than 'Configuration` will render this config read-only.
    /// <br/><br/><b>Expected Behaviour</b>:
    /// <br/><b>[<see cref="RunState.Unloaded"/>|<see cref="RunState.Parsed"/>]</b>: Read-Only.
    /// <br/><b>[<see cref="RunState.Configuration"/>]</b>: Can only be changed at the Main Menu (not in a lobby).
    /// <br/><b>[<see cref="RunState.Running"/>]</b>: Can be changed at the Main Menu and while a lobby is active. 
    /// </summary>
    RunState EditableStates { get; }
    /// <summary>
    /// Network synchronization rules for this config.
    /// </summary>
    NetSync NetSync { get; }
}
