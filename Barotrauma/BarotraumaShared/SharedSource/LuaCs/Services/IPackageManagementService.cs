using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPackageManagementService : IService
{
    void AddPackages(ref ReadOnlySpan<(ContentPackage, bool)> packages, 
        bool executeImmediately = false, 
        bool errorOnFailures = false, 
        bool errorOnExistingPackageFound = false);
    void LoadPackages(bool onlyUnloadedPackages = true, bool rescanPackages = false);
    void UnloadPackages(bool errorOnFailures = true);
    bool IsPackageLoaded(ContentPackage package);
    bool CheckDependencyLoaded(IPackageDependencyInfo info);
    bool CheckDependenciesLoaded(IEnumerable<IPackageDependencyInfo> infos, out IReadOnlyList<IPackageDependencyInfo> missingPackages);
    bool CheckEnvironmentSupported(IPlatformInfo platform);
}
