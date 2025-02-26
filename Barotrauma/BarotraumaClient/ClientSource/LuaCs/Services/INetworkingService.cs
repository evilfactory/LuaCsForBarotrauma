using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Services;

internal partial interface INetworkingService : IReusableService
{
    void NetMessageReceived(IReadMessage message, ServerPacketHeader header);
}
