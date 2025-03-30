using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Barotrauma.Steam;

namespace Barotrauma.LuaCs.Data;

#region ModConfigurationInfo

public partial record ModConfigInfo : IModConfigInfo
{
    public ContentPackage Package { get; init; }
    public string PackageName { get; init; }
    public ImmutableArray<IAssemblyResourceInfo> Assemblies { get; init; }
    public ImmutableArray<ILocalizationResourceInfo> Localizations { get; init; }
    public ImmutableArray<ILuaScriptResourceInfo> LuaScripts { get; init; }
    public ImmutableArray<IConfigResourceInfo> Configs { get; init; }
    public ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles { get; init; }
}

#endregion

#region DataContracts_Resources

public record AssemblyResourcesInfo(ImmutableArray<IAssemblyResourceInfo> Assemblies) : IAssembliesResourcesInfo;
public record LocalizationResourcesInfo(ImmutableArray<ILocalizationResourceInfo> Localizations) : ILocalizationsResourcesInfo;
public record LuaScriptsResourcesInfo(ImmutableArray<ILuaScriptResourceInfo> LuaScripts) : ILuaScriptsResourcesInfo;
public record ConfigResourcesInfo(ImmutableArray<IConfigResourceInfo> Configs) : IConfigsResourcesInfo;
public record ConfigProfilesResourcesInfo(ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles) : IConfigProfilesResourcesInfo;

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
    public ImmutableArray<IPackageDependency> Dependencies { get; init; }
    public bool Optional { get; init; }
}

public record PackageDependency : IPackageDependency
{
    public PackageDependency(ContentPackage package, IPackageInfo dependencyInfo, string internalName)
    {
        Dependency = dependencyInfo ?? throw new ArgumentNullException(nameof(dependencyInfo));
        OwnerPackage = package ?? throw new ArgumentNullException(nameof(package));
        InternalName = internalName ?? throw new ArgumentNullException(nameof(internalName));
    }
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    public IPackageInfo Dependency { get; init; }
    public override int GetHashCode() => Dependency.GetHashCode();
    
}

public record PackageInfo : IPackageInfo
{
    public string Name { get; private set; }
    public ulong SteamWorkshopId { get; private set; }
    public uint Id { get; private set; }
    
    private readonly Func<IPackageInfo, ContentPackage> _getPackage;

    public ContentPackage GetPackage() => _getPackage?.Invoke(this) ?? null;

    public void UpdateInfo(string name, ulong steamId, uint packageId)
    {
        if (name.IsNullOrWhiteSpace() || steamId == 0 || packageId == 0)
        {
            throw new ArgumentException(
                $"{nameof(PackageInfo)}: You cannot update a package with an invalid name or steam id with a valid id, or vice-versa.");
        }

        Name = name;
        SteamWorkshopId = steamId;
        Id = packageId;
    }

    public PackageInfo(ContentPackage package, uint id, Func<IPackageInfo, ContentPackage> getPackage)
    {
        if (package is null)
            throw new ArgumentNullException($"{nameof(PackageInfo)}: package is null");
        if (id == 0)
            throw new ArgumentNullException($"{nameof(PackageInfo)}: id is zero.");
        
        this.Name = package.Name;
        this.SteamWorkshopId = package.TryExtractSteamWorkshopId(out var sId) ? sId.Value : 0;
        this.Id = id;
        this._getPackage = getPackage;
    }
    
    public PackageInfo(string name, ulong steamWorkshopId, uint id, Func<IPackageInfo, ContentPackage> getPackage)
    {
        Name = !name.IsNullOrWhiteSpace() ? name : throw new ArgumentNullException($"{nameof(PackageInfo)}: name cannot be null or empty.");
        SteamWorkshopId = steamWorkshopId != 0 ? steamWorkshopId : throw new ArgumentNullException($"{nameof(PackageInfo)}: steam id cannot be 0.");
        this.Id = id;
        this._getPackage = getPackage;
    }

    public PackageInfo(string name, uint id, Func<IPackageInfo, ContentPackage> getPackage)
    {
        Name = name ?? throw new ArgumentNullException($"{nameof(PackageInfo)}: name cannot be null or empty.");
        this.SteamWorkshopId = 0;
        this.Id = id;
        this._getPackage = getPackage;
    }

    public PackageInfo(ulong steamWorkshopId, uint id, Func<IPackageInfo, ContentPackage> getPackage)
    {
        SteamWorkshopId = steamWorkshopId != 0 ? steamWorkshopId : throw new ArgumentNullException($"{nameof(PackageInfo)}: steamid cannot be 0.");
        this.Id = id;
        this._getPackage = getPackage;
    }
    
    public override int GetHashCode()
    {
        return (int)Id;
    }
    
    public virtual bool Equals(PackageInfo other)
    {
        return ((IEquatable<IPackageInfo>)this).Equals(other);
    }
}



public record ConfigResourceInfo : IConfigResourceInfo
{
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public bool Optional { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependency> Dependencies { get; init; }
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
}

public record ConfigProfileResourceInfo : IConfigProfileResourceInfo
{
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public bool Optional { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependency> Dependencies { get; init; }
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
}

public record LocalizationResourceInfo : ILocalizationResourceInfo
{
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependency> Dependencies { get; init; }
    public bool Optional { get; init; }
}

public readonly struct LuaScriptScriptResourceInfo : ILuaScriptResourceInfo
{
    public ContentPackage OwnerPackage { get; init; }
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependency> Dependencies { get; init; }
    public bool Optional { get; init; }
    public string InternalName { get; init; }
    public bool IsAutorun { get; init; }
}

#endregion

#region DataContracts_ParsedInfo

public record LocalizationInfo : ILocalizationInfo
{
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    public int LoadPriority { get; init; }
    public string Key { get; init; }
    public IReadOnlyList<(CultureInfo Culture, string Value)> Translations { get; init; }
}

#endregion
