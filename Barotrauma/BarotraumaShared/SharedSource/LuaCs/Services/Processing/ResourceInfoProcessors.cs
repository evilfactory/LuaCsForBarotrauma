using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services.Processing;

public partial class ResourceInfoArrayPacker :
    IProcessorService<IReadOnlyList<IAssemblyResourceInfo>, IAssembliesResourcesInfo>,
    IProcessorService<IReadOnlyList<IConfigResourceInfo>, IConfigsResourcesInfo>,
    IProcessorService<IReadOnlyList<IConfigProfileResourceInfo>, IConfigProfilesResourcesInfo>,
    IProcessorService<IReadOnlyList<ILocalizationResourceInfo>, ILocalizationsResourcesInfo>,
    IProcessorService<IReadOnlyList<ILuaScriptResourceInfo>, ILuaScriptsResourcesInfo>
{
    private bool _isDisposed;
    public IAssembliesResourcesInfo Process(IReadOnlyList<IAssemblyResourceInfo> src)
    {
        return new AssemblyResourcesInfo(src.ToImmutableArray());
    }

    public IConfigsResourcesInfo Process(IReadOnlyList<IConfigResourceInfo> src)
    {
        return new ConfigResourcesInfo(src.ToImmutableArray());
    }

    public IConfigProfilesResourcesInfo Process(IReadOnlyList<IConfigProfileResourceInfo> src)
    {
        return new ConfigProfilesResourcesInfo(src.ToImmutableArray());
    }

    public ILocalizationsResourcesInfo Process(IReadOnlyList<ILocalizationResourceInfo> src)
    {
        return new LocalizationResourcesInfo(src.ToImmutableArray());
    }

    public ILuaScriptsResourcesInfo Process(IReadOnlyList<ILuaScriptResourceInfo> src)
    {
        return new LuaScriptsResourcesInfo(src.ToImmutableArray());
    }

    public void Dispose()
    {
        // Stateless class
        GC.SuppressFinalize(this);
        IsDisposed = true;
    }

    public bool IsDisposed
    {
        get => _isDisposed;
        set => _isDisposed = value;
    }
}
