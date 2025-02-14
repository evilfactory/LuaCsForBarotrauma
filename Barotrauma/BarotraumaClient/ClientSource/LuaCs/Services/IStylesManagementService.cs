using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IStylesManagementService : IReusableService
{
    Task<FluentResults.Result> LoadStylesAsync(ImmutableArray<IStylesResourceInfo> styles);
    FluentResults.Result<IStylesService> GetStylesService(ContentPackage package);
    Task<FluentResults.Result> DisposeAllStyles();
    Task<FluentResults.Result> DisposeStylesForPackage(ContentPackage package);
}
