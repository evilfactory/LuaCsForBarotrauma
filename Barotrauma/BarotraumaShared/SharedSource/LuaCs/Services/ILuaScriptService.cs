using System;

namespace Barotrauma.LuaCs.Services;

public interface ILuaScriptService
{
    #region Type_Registration

    void RegisterSafeType(Type type);
    void UnregisterSafeType(Type type);
    void UnregisterAllTypes();
    
    #endregion

    #region Script_File_Runner

    void AddScriptFiles(string[] filePaths);
    void RemoveScriptFiles(string[] filePaths);
    void RunLoadedScripts();

    #endregion
}
