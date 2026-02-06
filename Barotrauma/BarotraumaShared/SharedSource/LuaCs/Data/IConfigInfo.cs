using System;
using System.Xml.Linq;
using Barotrauma.LuaCs;
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
    string DataType { get; }
    /// <summary>
    /// The 'Setting' XML element.
    /// </summary>
    XElement Element { get; }
    /// <summary>
    /// In what <see cref="RunState"/>(s) is this config editable. Will be editable in the selected state, and lower value states.
    /// <br/><br/>
    /// <b>[Important]</b><br/> Setting this to value lower than 'Configuration` will render this config read-only.
    /// <br/><br/><b>Expected Behaviour</b>:
    /// <br/><b>[<see cref="RunState.Unloaded"/>|<see cref="RunState.Unloaded"/>]</b>: Read-Only.
    /// <br/><b>[<see cref="RunState.LoadedNoExec"/>]</b>: Can only be changed at the Main Menu (not in a lobby).
    /// <br/><b>[<see cref="RunState.Running"/>]</b>: Can be changed at the Main Menu and while a lobby is active. 
    /// </summary>
    RunState EditableStates { get; }
    /// <summary>
    /// Network synchronization rules for this config.
    /// </summary>
    NetSync NetSync { get; }
}
