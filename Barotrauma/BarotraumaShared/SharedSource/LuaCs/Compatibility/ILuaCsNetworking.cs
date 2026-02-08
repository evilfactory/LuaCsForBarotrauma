namespace Barotrauma.LuaCs.Compatibility;

public interface ILuaCsNetworking : ILuaCsShim
{
    void Receive(string netId, LuaCsAction action);
}
