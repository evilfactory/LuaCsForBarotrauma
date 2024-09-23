using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IContentPackageService : IService
{
    bool TryLoadPackageData(ContentPackage package);
    ModConfigInfo GetModConfigData();
    ContentPackage TryFindPackage(string packageName, bool prioritizeLocal = true);
    ContentPackage TryFindPackage(int steamId);
    ContentPath GetContentPath();
}
