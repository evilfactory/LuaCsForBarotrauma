using System;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Data;

// TODO: Finish
public partial interface IConfigInfo : IDataInfo
{
    /// <summary>
    /// Specifies the data type this should be initialized to (ie. string, int, vector, etc.)
    /// Custom types can be registered by mods.
    /// </summary>
    Type DataType { get; }
    /// <summary>
    /// String version of the default value. 
    /// </summary>
    string DefaultValue { get; }
    /// <summary>
    /// The value the last time this config was saved, if found in /data/. 
    /// </summary>
    string StoredValue { get; }
    /// <summary>
    /// Custom data storage for other type-specific information needed. IE. Used to store the min,
    /// max and step values for the <b>IConfigRangeEntry(T)</b>.
    /// </summary>
    string CustomParameters { get; }
    /// <summary>
    /// <b>[Multiplayer]</b><br/>
    /// What permissions do clients require to change this setting.
    /// </summary>
    ClientPermissions RequiredPermissions { get; }
    /// <summary>
    /// In what <see cref="RunState"/>s is this config editable.
    /// <br/>
    /// Note: Setting this to value lower than 'Configuration` will render this config read-only.
    /// </summary>
    RunState CanEditStates { get; }
    /// <summary>
    /// Network synchronization rules for this config.
    /// </summary>
    NetSync NetSync { get; }
    /// <summary>
    /// User friendly name or Localization Token.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// User friendly description or Localization Token.
    /// </summary>
    string Description { get; }
}
