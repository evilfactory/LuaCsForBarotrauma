using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IContentPackageProcessorService : IService
{
    bool TryLoadPackageData(ContentPackage package);
    ModConfigData GetModConfigData();
    ContentPackage TryFindPackage(string packageName, bool prioritizeLocal = true);
    ContentPackage TryFindPackage(int steamId);
    ContentPath GetContentPath();
}
