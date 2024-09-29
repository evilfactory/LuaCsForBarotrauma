using System;

namespace Barotrauma.LuaCs.Services;

public class PackageManagementService : IPackageManagementService
{
    private Func<IContentPackageService> GetPackageServiceInstance { get; init; }

    public PackageManagementService(Func<IContentPackageService> getPackageService)
    {
        this.GetPackageServiceInstance = getPackageService;
    }
    
    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
