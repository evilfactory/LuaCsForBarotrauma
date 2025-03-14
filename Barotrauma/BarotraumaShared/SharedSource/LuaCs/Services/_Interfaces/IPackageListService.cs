using System.Collections.Generic;

namespace Barotrauma.LuaCs.Services;

public interface IPackageListService : IService
{
    IEnumerable<ContentPackage> GetEnabledContentPackages();
    IEnumerable<ContentPackage> GetAllContentPackages();
}
