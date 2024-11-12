using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public class PackageManagementService : IPackageManagementService
{
    private readonly Func<IPackageService> _contentPackageServiceFactory;
    private readonly Lazy<IAssemblyManagementService> _assemblyManagementService;

    public PackageManagementService(
        Func<IPackageService> getPackageService,
        Lazy<IAssemblyManagementService> assemblyManagementService)
    {
        this._contentPackageServiceFactory = getPackageService;
        this._assemblyManagementService = assemblyManagementService;
    }


    public void Dispose()
    {
        // TODO release managed resources here
    }

    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }

    public bool IsAssemblyLoadedGlobal(string friendlyName)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result AddPackages(ImmutableArray<LoadablePackage> packages)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result LoadPackages(bool onlyUnloadedPackages = true, bool rescanPackages = false)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result UnloadPackages(bool errorOnFailures = true)
    {
        throw new NotImplementedException();
    }

    public bool IsPackageLoaded(ContentPackage package)
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
}
