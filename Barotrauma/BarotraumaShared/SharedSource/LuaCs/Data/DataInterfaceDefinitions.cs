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
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    public string FolderPath { get; init; }
    public string PackageName { get; init; }
    public ulong SteamWorkshopId { get; init; }
    public ContentPackage DependencyPackage { get; init; }
    public bool IsMissing { get; init; }
    public bool IsWorkshopInstallation { get; init; }

    public virtual bool Equals(DependencyInfo other) => Equals(this, other);

    public override int GetHashCode()
    {
        if (DependencyPackage is not null)
            return DependencyPackage.GetHashCode();
        if (SteamWorkshopId != 0)
            return SteamWorkshopId.GetHashCode();
        if (!PackageName.IsNullOrWhiteSpace() && !FolderPath.IsNullOrWhiteSpace())
            return string.Concat(PackageName, FolderPath).GetHashCode();
        if (!InternalName.IsNullOrWhiteSpace() && !FolderPath.IsNullOrWhiteSpace())
            return string.Concat(InternalName, FolderPath).GetHashCode();
        
        return base.GetHashCode();
    }

    public bool Equals(IPackageDependencyInfo x, IPackageDependencyInfo y)
    {
        if (x is null)
            return false;
        if (y is null)
            return false;
        if (x == y)
            return true;
        
        if (x.DependencyPackage is not null && y.DependencyPackage is not null)
            return y.DependencyPackage == x.DependencyPackage;
        
        if (!x.FolderPath.IsNullOrWhiteSpace()
            && !y.FolderPath.IsNullOrWhiteSpace()
            && y.FolderPath == x.FolderPath)
            return true;
        
        if (!x.FolderPath.IsNullOrWhiteSpace() != !y.FolderPath.IsNullOrWhiteSpace()) 
            return false;
        
        if (!x.PackageName.IsNullOrWhiteSpace() 
            && !y.PackageName.IsNullOrWhiteSpace() 
            && y.PackageName == x.PackageName)
            return true;
        
        if (x.SteamWorkshopId != 0 && y.SteamWorkshopId == x.SteamWorkshopId)
            return true;

        return false;
    }

    public int GetHashCode(IPackageDependencyInfo obj)
    {
        throw new NotImplementedException();
    }
}

public record LocalizationResourceInfo : ILocalizationResourceInfo
{
    public string InternalName { get; init; }
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
