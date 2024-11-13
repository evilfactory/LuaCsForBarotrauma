using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services.Compatibility;

internal partial interface ILuaCsNetworking : ILuaCsShim
{
    public void NetMessageReceived(IReadMessage netMessage, ServerPacketHeader header, Client client = null);
}
