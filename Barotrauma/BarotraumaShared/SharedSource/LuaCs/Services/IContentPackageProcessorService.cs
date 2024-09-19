namespace Barotrauma.LuaCs.Services;

public interface IContentPackageProcessorService : IService
{
    ModConfigData GetModConfigData(ContentPackage package);
    ContentPackage TryFindPackage(string packageName, bool prioritizeLocal = true);
    ContentPackage TryFindPackage(int steamId);
    ContentPath GetContentPath(ContentPackage package);
}
