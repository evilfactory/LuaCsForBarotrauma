using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services.Processing;

public interface IModConfigCreatorService : IService
{
    FluentResults.Result<IModConfigInfo> BuildConfigForPackage(ContentPackage package);
    FluentResults.Result<IModConfigInfo> BuildConfigFromManifest(string manifestPath);
}
