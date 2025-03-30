using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services.Processing;

public class ResourceInfoLoaders : IConverterServiceAsync<ILocalizationResourceInfo, ILocalizationInfo>
{
    private IStorageService _storageService;

    public ResourceInfoLoaders(IStorageService storageService)
    {
        _storageService = storageService;
    }
    
    public void Dispose()
    {
        IsDisposed = true;
    }

    private int _isDisposed;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    
    public async Task<Result<ILocalizationInfo>> TryParseResourceAsync(ILocalizationResourceInfo src)
    {
        try
        {
            if (src is null || src.FilePaths.IsDefaultOrEmpty)
                return FluentResults.Result.Fail($"{nameof(TryParseResourceAsync)}: Source was null or empty.");

            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail($"Unable to load file. Error: {e.Message}.");
        } 
        
        
    }

    public async Task<ImmutableArray<Result<ILocalizationInfo>>> TryParseResourcesAsync(IEnumerable<ILocalizationResourceInfo> sources)
    {
        var results = new ConcurrentQueue<Result<ILocalizationInfo>>();

        var src = sources.ToImmutableArray();
        if (!src.Any())
            return ImmutableArray<Result<ILocalizationInfo>>.Empty;

        await src.ParallelForEachAsync(async loc =>
        {
            var res = await TryParseResourceAsync(loc);
            results.Enqueue(res);
        }, 2);  // we only need 2 parallels to buffer against disk loading.
        
        return results.ToImmutableArray();
    }
}
