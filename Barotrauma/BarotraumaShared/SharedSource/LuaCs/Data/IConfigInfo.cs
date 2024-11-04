using System;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Data;

// TODO: Finish
public partial interface IConfigInfo : IDataInfo
{
    ConfigDataType Type { get; }
    string DefaultValue { get; }
    ClientPermissions RequiredPermissions { get; }
}

public enum ConfigDataType
{
    Boolean,
    Int32,
    Int64,
    Single,
    Double,
    String,
    Color,
    Vector2,
    Vector3,
    List,
    RangeInt32,
    RangeSingle,
    ControlInput
}
