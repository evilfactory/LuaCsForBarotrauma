using System.Collections.Generic;

namespace Barotrauma.LuaCs.Services;

public sealed class PackageListRetrievalService : IPackageListRetrievalService
{
    public void Dispose()
    {
        // stateless service
        return;
    }

    public void CheckDisposed()
    {
        // stateless service
        return;
    }

    public bool IsDisposed => false;

    public IEnumerable<ContentPackage> GetEnabledContentPackages()
    {
        return ContentPackageManager.EnabledPackages.All;
    }

    public IEnumerable<ContentPackage> GetAllContentPackages()
    {
        return ContentPackageManager.AllPackages;
    }
}
