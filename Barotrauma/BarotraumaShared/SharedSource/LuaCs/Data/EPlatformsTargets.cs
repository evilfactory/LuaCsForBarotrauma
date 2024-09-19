using System;

namespace Barotrauma.LuaCs.Data;

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

[Flags]
public enum TargetRunMode
{
    ClientEnabled = 0x1,
    ClientAlways = 0x2,
    ServerEnabled = 0x4,
    ServerAlways = 0x8
}
