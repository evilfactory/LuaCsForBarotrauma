using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Barotrauma.LuaCs.Data;

#region ModConfigurationInfo

public partial record ModConfigInfo : IModConfigInfo
{
    public ContentPackage Package { get; init; }
    public string PackageName { get; init; }
    public TargetRunMode RunModes { get; init; }
    
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IAssemblyResourceInfo> Assemblies { get; init; }
    public ImmutableArray<ILocalizationResourceInfo> Localizations { get; init; }
    public ImmutableArray<ILuaResourceInfo> LuaScripts { get; init; }
    public ImmutableArray<IConfigResourceInfo> Configs { get; init; }
    public ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles { get; init; }
}

#endregion

#region DataContracts

public record AssemblyResourceInfo : IAssemblyResourceInfo
{
    public ContentPackage OwnerPackage { get; init; }
    public string FriendlyName { get; init; }
    public bool IsScript { get; init; }
    public string InternalName { get; init; }
    public bool LazyLoad { get; init; }
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
    public bool Optional { get; init; }
}

public record DependencyInfo : IPackageDependencyInfo
{
    public ContentPackage OwnerPackage { get; init; }
    public string FolderPath { get; init; }
    public string PackageName { get; init; }
    public ulong SteamWorkshopId { get; init; }
    public ContentPackage DependencyPackage { get; init; }
}

public record LocalizationResourceInfo : ILocalizationResourceInfo
{
    public ContentPackage OwnerPackage { get; init; }
    public CultureInfo TargetCulture { get; init; }
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
    public bool Optional { get; init; }
}

public readonly struct LuaScriptResourceInfo : ILuaResourceInfo
{
    public ContentPackage OwnerPackage { get; init; }
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
    public bool Optional { get; init; }
    public string InternalName { get; init; }
    public bool LazyLoad { get; init; }
}

#endregion
