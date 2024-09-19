namespace Barotrauma.LuaCs.Services;

public interface IPluginManagementService : IService
{
    bool IsAssemblyLoadedGlobal(string friendlyName);
    
}
