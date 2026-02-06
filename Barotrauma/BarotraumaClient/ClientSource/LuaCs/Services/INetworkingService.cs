using Barotrauma.Networking;

namespace Barotrauma.LuaCs;

internal partial interface INetworkingService : IReusableService
{
    void NetMessageReceived(IReadMessage message, ServerPacketHeader header);
}
