using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Barotrauma.LuaCs.Data;

#region ModConfigurationInfo

[StructLayout(LayoutKind.Auto)] // because we have a partial readonly struct
public readonly partial struct ModConfigInfo : IModConfigInfo
{
    public ContentPackage Package { get; init; }
    public string PackageName { get; init; }
    public TargetRunMode RunModes { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    
    // metadata
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
    public ImmutableArray<IAssemblyResourceInfo> LoadableAssemblies { get; init; }
    public ImmutableArray<ILocalizationResourceInfo> LocalizationFiles { get; init; }
}

#endregion

#region DataContracts

public readonly struct AssemblyResourceInfo : IAssemblyResourceInfo
{
    public string FriendlyName { get; init; }
    public bool IsScript { get; init; }
    public string InternalName { get; init; }
    public bool LazyLoad { get; init; }
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
}

public readonly struct DependencyInfo : IPackageDependencyInfo
{
    public string FolderPath { get; init; }
    public string PackageName { get; init; }
    public ulong SteamWorkshopId { get; init; }
    public ContentPackage DependencyPackage { get; init; }
    public bool Optional { get; init; }
}

public readonly struct LocalizationResourceInfo : ILocalizationResourceInfo
{
    public CultureInfo TargetCulture { get; init; }
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
}

public readonly struct LuaScriptResourceInfo : ILuaResourceInfo
{
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
    public string InternalName { get; init; }
    public bool LazyLoad { get; init; }
}

#endregion
