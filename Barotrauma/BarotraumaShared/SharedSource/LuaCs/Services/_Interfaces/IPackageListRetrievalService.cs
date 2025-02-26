using System.Collections.Generic;

namespace Barotrauma.LuaCs.Services;

public interface IPackageListRetrievalService : IService
{
    IEnumerable<ContentPackage> GetEnabledContentPackages();
    IEnumerable<ContentPackage> GetAllContentPackages();
}
