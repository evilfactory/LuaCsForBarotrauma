using System;
using System.Collections.Generic;

namespace Barotrauma.LuaCs.Services;

public class PackageManagementService : IPackageManagementService, IPluginManagementService
{
    private readonly Func<IContentPackageService> _contentPackageServiceFactory;

    public PackageManagementService(Func<IContentPackageService> getPackageService)
    {
        this._contentPackageServiceFactory = getPackageService;
    }
    
    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
