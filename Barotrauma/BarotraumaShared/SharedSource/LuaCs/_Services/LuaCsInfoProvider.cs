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
    public bool IsCsEnabled => LuaCsSetup.Instance.IsCsEnabled;
    public bool HideUserNamesInLogs => LuaCsSetup.Instance.HideUserNamesInLogs;
    public bool UseCaching => LuaCsSetup.Instance.UseCaching;
    public RunState CurrentRunState => LuaCsSetup.Instance.CurrentRunState;
    public ContentPackage LuaCsForBarotraumaPackage
    {
        get
        {
            return ContentPackageManager.EnabledPackages.Regular.FirstOrDefault(cp => cp.NameMatches(LuaCsSetup.PackageId), null)
                               ?? ContentPackageManager.LocalPackages.FirstOrDefault(cp => cp.NameMatches(LuaCsSetup.PackageId))
                               ?? ContentPackageManager.WorkshopPackages.FirstOrDefault(cp => cp.NameMatches(LuaCsSetup.PackageId));
        }
    }
}
