using System;
using System.Collections.Generic;
using Barotrauma.LuaCs.Data;

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

    public bool IsAssemblyLoadedGlobal(string friendlyName)
    {
        throw new NotImplementedException();
    }

    public bool CheckDependencyLoaded(IPackageDependencyInfo info)
    {
        throw new NotImplementedException();
    }

    public bool CheckDependenciesLoaded(IEnumerable<IPackageDependencyInfo> infos, out IReadOnlyList<IPackageDependencyInfo> missingPackages)
    {
        throw new NotImplementedException();
    }

    public bool CheckEnvironmentSupported(IPlatformInfo platform)
    {
        throw new NotImplementedException();
    }

    public bool CheckDependenciesLoaded(IEnumerable<IPackageDependencyInfo> infos)
    {
        throw new NotImplementedException();
    }
}
