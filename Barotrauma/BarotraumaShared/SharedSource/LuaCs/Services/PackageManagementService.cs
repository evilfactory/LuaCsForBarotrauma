using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public partial class PackageManagementService : IPackageManagementService
{
    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    private int _isDisposed = 0;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref  _isDisposed);
        private set => ModUtils.Threading.SetBool(ref  _isDisposed, value);
    }
    
    
    public FluentResults.Result Reset()
    {
        throw new System.NotImplementedException();
    }

    public ImmutableArray<IConfigResourceInfo> Configs { get; }
    public ImmutableArray<ILuaScriptResourceInfo> LuaScripts { get; }
    public ImmutableArray<IAssemblyResourceInfo> Assemblies { get; }
    public async Task<FluentResults.Result> LoadPackageInfosAsync(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }

    public async Task<IReadOnlyList<(ContentPackage, FluentResults.Result)>> LoadPackagesInfosAsync(IReadOnlyList<ContentPackage> packages)
    {
        throw new System.NotImplementedException();
    }

    public IReadOnlyList<ContentPackage> GetAllLoadedPackages()
    {
        throw new System.NotImplementedException();
    }

    public bool IsPackageLoaded(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }

    public ImmutableArray<T> FilterUnloadableResources<T>(IReadOnlyList<T> resources, bool enabledPackagesOnly = false) where T : IResourceInfo
    {
        throw new System.NotImplementedException();
    }

    public void DisposePackageInfos(ContentPackage package)
    {
        throw new System.NotImplementedException();
    }

    public void DisposePackagesInfos(IReadOnlyList<ContentPackage> packages)
    {
        throw new System.NotImplementedException();
    }

    public Result<IAssembliesResourcesInfo> GetAssembliesInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }

    public Result<IConfigsResourcesInfo> GetConfigsInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }

    public Result<ILuaScriptsResourcesInfo> GetLuaScriptsInfos(ContentPackage package, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }

    public Result<IAssembliesResourcesInfo> GetAssembliesInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }

    public Result<IConfigsResourcesInfo> GetConfigsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }

    public Result<ILuaScriptsResourcesInfo> GetLuaScriptsInfos(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }

    public async Task<Result<IAssembliesResourcesInfo>> GetAssembliesInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }

    public async Task<Result<IConfigsResourcesInfo>> GetConfigsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }

    public async Task<Result<ILuaScriptsResourcesInfo>> GetLuaScriptsInfosAsync(IReadOnlyList<ContentPackage> packages, bool onlySupportedResources = true)
    {
        throw new System.NotImplementedException();
    }
}
