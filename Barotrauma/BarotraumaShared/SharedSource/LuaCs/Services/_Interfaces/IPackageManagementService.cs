using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public interface IPackageManagementService : IReusableService
{
    public FluentResults.Result LoadPackageInfo(ContentPackage package);
    public FluentResults.Result LoadPackagesInfo(ImmutableArray<ContentPackage> packages);
    public FluentResults.Result ExecuteLoadedPackages(ImmutableArray<ContentPackage> executionOrder);
    public FluentResults.Result StopRunningPackages();
    public FluentResults.Result UnloadPackage(ContentPackage package);      
    public FluentResults.Result UnloadPackages(ImmutableArray<ContentPackage> packages);
    public FluentResults.Result UnloadAllPackages();
    public ImmutableArray<ContentPackage> GetAllLoadedPackages();
    public bool IsPackageRunning(ContentPackage package);
    public ImmutableArray<ContentPackage> GetLoadedAssemblyPackages();
}
