using System;
// ReSharper disable InconsistentNaming

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
