using System;
using Barotrauma.LuaCs.Networking;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Data;

// TODO: Finish
public partial interface IConfigInfo : IDataInfo
{
    /// <summary>
    /// Specifies the data type this should be initialized to (ie. string, int, vector, etc.)
    /// Custom types can be registered by mods.
    /// </summary>
    string DataType { get; }
    string DefaultValue { get; }
    string StoredValue { get; }
    ClientPermissions RequiredPermissions { get; }
    /// <summary>
    /// Whether a value can be changed at runtime.
    /// </summary>
    bool IsReadOnly { get; }
    NetSync NetSync { get; }
}
