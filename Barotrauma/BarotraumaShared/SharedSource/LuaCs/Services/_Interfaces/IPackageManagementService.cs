using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPackageManagementService : IReusableService
{
    public FluentResults.Result LoadPackageInfo(ContentPackage package);
    public ImmutableArray<(ContentPackage Package, FluentResults.Result LoadSuccessResult)> LoadPackagesInfo(IReadOnlyCollection<ContentPackage> packages);
    public ImmutableArray<(ContentPackage Package, FluentResults.Result ExecutionResult)> ExecuteLoadedPackages();
    public ImmutableArray<(ContentPackage Package, FluentResults.Result StopExecutionResult)> StopRunningPackages();
    public FluentResults.Result UnloadPackage(ContentPackage package);
    public ImmutableArray<(ContentPackage Package, FluentResults.Result UnloadSuccessResult)> UnloadPackages(IReadOnlyCollection<ContentPackage> packages);
    public ImmutableArray<(ContentPackage Package, FluentResults.Result UnloadSuccessResult)> UnloadAllPackages();
}
