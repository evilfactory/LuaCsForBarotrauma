using System;
using System.Collections.Generic;
using System.Linq;

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
    public ContentPackage LuaCsForBarotraumaPackage
    {
        get
        {
            var luaCs = FirstOrDefaultLua(ContentPackageManager.EnabledPackages.All);
            if (luaCs == null)
            {
                luaCs = FirstOrDefaultLua(ContentPackageManager.LocalPackages.Regular);
            }

            if (luaCs == null)
            {
                luaCs = FirstOrDefaultLua(ContentPackageManager.WorkshopPackages.Regular);
            }
            
            return luaCs;

            ContentPackage FirstOrDefaultLua(IEnumerable<ContentPackage> packages)
            {
                return packages.FirstOrDefault(p =>
                    p.Name.Equals("LuaCsForBarotrauma", StringComparison.InvariantCultureIgnoreCase)
                    || p.Name.Equals("Lua for Barotrauma", StringComparison.InvariantCultureIgnoreCase));
            }
        }
    }
}
