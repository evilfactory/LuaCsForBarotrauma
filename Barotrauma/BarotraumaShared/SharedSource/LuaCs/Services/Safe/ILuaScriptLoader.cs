using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using FluentResults;
using MoonSharp.Interpreter.Loaders;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ILuaScriptLoader : IService, IScriptLoader
{
    void ClearCaches();
    Task<Result<ImmutableArray<(ContentPath Path, Result<string>)>>> CacheResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourceInfos);
}
