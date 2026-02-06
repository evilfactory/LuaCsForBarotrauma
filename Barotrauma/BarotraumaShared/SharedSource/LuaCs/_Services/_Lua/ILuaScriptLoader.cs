using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using FluentResults;
using MoonSharp.Interpreter.Loaders;

namespace Barotrauma.LuaCs;

public interface ILuaScriptLoader : IService, IScriptLoader, ISafeStorageValidation
{
    void ClearCaches();
    Task<Result<ImmutableArray<(ContentPath Path, Result<string>)>>> CacheResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourceInfos);
}
