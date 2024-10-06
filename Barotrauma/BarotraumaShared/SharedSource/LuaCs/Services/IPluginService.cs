using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPluginService : IService
{
    bool IsPlatformSupported(Platform supportedPlatforms);
}
