using System;

namespace Barotrauma.LuaCs.Networking;

public partial interface INetCallback
{
    public ushort CallbackId { get; }
}

#if SERVER
public partial interface INetCallback
{
    
}
#endif
