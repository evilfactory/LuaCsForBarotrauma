namespace Barotrauma.LuaCs;

public sealed class LuaCsInfoProvider : ILuaCsInfoProvider
{
    public void Dispose()
    {
        // stateless service
    }

    public bool IsDisposed => false;
    public bool IsCsEnabled => GameMain.LuaCs.IsCsEnabled;
    public bool DisableErrorGUIOverlay => GameMain.LuaCs.DisableErrorGUIOverlay;
    public bool HideUserNamesInLogs => GameMain.LuaCs.HideUserNamesInLogs;
    public ulong LuaForBarotraumaSteamId => GameMain.LuaCs.LuaForBarotraumaSteamId;
    public bool RestrictMessageSize => GameMain.LuaCs.RestrictMessageSize;
    public string LocalDataSavePath =>  GameMain.LuaCs.LocalDataSavePath;
    public RunState CurrentRunState => GameMain.LuaCs.CurrentRunState;
}
