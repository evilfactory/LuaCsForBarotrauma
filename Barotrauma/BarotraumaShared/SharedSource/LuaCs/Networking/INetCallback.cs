using System;

namespace Barotrauma.LuaCs.Services;

public partial interface INetCallback
{
    public ushort CallbackId { get; }
}

#if SERVER
public partial interface INetCallback
{
    
}
#endif
