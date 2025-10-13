using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services.Processing;

public class StylesManagementService : IStylesManagementService
{
    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public bool IsDisposed { get; set; }
    public FluentResults.Result Reset()
    {
        throw new System.NotImplementedException();
    }

    public async Task<FluentResults.Result> LoadStylesAsync(ImmutableArray<IStylesResourceInfo> styles)
    {
        throw new System.NotImplementedException();
    }

    public Result<IStylesService> GetStylesService(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }

    public async Task<FluentResults.Result> DisposeAllStyles()
    {
        throw new System.NotImplementedException();
    }

    public async Task<FluentResults.Result> DisposeStylesForPackage(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }
}
