using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface ILegacyConfigService : IService
{
    bool TryBuildModConfigFromLegacy(ContentPackage package, out IModConfigInfo configInfo);
}
