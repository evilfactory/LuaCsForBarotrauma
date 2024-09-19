namespace Barotrauma.LuaCs.Services;

public interface IClientLoggerService : IService
{
    void AddToGUIUpdateList();
    void ShowErrorOverlay(string message, float time = 5f, float duration = 1.5f);
}
