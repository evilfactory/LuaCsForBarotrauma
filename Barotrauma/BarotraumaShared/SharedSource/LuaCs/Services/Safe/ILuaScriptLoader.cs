using MoonSharp.Interpreter.Loaders;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ILuaScriptLoader : IService, IScriptLoader
{
    void ClearCaches();
}
