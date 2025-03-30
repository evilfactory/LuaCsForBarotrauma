using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;

namespace Barotrauma.LuaCs.Services;

public interface IPackageInfoLookupService : IReusableService
{
    bool IsPackageEnabled(ContentPackage package);
    Task<FluentResults.Result<IPackageInfo>> Lookup(string packageName);
    Task<FluentResults.Result<IPackageInfo>> Lookup(string packageName, ulong steamWorkshopId);
    Task<FluentResults.Result<IPackageInfo>> Lookup(ulong steamWorkshopId);
    Task<FluentResults.Result<IPackageInfo>> Lookup(ContentPackage package);
    void RefreshPackageLists();
}
