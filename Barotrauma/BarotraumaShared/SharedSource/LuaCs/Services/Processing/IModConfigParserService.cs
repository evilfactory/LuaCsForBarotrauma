using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services.Processing;

public interface IModConfigParserService : IService
{
    FluentResults.Result<IModConfigInfo> BuildConfigForPackage(ContentPackage package);
}
