using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPackageManagementService : IService
{
    bool CheckDependencyLoaded(IPackageDependencyInfo info);
    bool CheckDependenciesLoaded(IEnumerable<IPackageDependencyInfo> infos, out IReadOnlyList<IPackageDependencyInfo> missingPackages);
    bool CheckEnvironmentSupported(IPlatformInfo platform, IResourceCultureInfo cultureInfos);
}
