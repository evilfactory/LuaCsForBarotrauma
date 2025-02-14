using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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
    public ImmutableArray<ILuaScriptResourceInfo> LuaScripts { get; init; }
    public ImmutableArray<IConfigResourceInfo> Configs { get; init; }
    public ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles { get; init; }
}

#endregion

#region DataContracts

public record AssemblyResourceInfo : IAssemblyResourceInfo
{
    public ContentPackage OwnerPackage { get; init; }
    public string FallbackPackageName { get; init; }
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
    public string FallbackPackageName { get; init; }
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
        if (!FallbackPackageName.IsNullOrWhiteSpace() && !FolderPath.IsNullOrWhiteSpace())
            return string.Concat(FallbackPackageName, FolderPath).GetHashCode();
        if (!InternalName.IsNullOrWhiteSpace() && !FolderPath.IsNullOrWhiteSpace())
            return string.Concat(InternalName, FolderPath).GetHashCode();
        
        return base.GetHashCode();
    }

    bool IEqualityComparer<IPackageDependencyInfo>.Equals(IPackageDependencyInfo x, IPackageDependencyInfo y) => DependencyInfo.Equals(x, y);
    
    public static bool operator ==(IPackageDependencyInfo x, DependencyInfo y) => y?.Equals(x) ?? false;
    public static bool operator !=(IPackageDependencyInfo x, DependencyInfo y) => y?.Equals(x) ?? false;
    public static bool Equals(IPackageDependencyInfo x, IPackageDependencyInfo y)
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
        
        if (!x.FallbackPackageName.IsNullOrWhiteSpace() 
            && !y.FallbackPackageName.IsNullOrWhiteSpace() 
            && y.FallbackPackageName == x.FallbackPackageName)
            return true;
        
        if (x.SteamWorkshopId != 0 && y.SteamWorkshopId == x.SteamWorkshopId)
            return true;

        return false;
    }
    
    /// <summary>
    /// Returns the hash code unique for the package reference.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    /// <remarks>The hash should only be collision-free when referring to different packages.</remarks>
    public int GetHashCode(IPackageDependencyInfo obj)
    {
        int hashCode = Seed;
        hashCode = ApplyHashString(hashCode, obj.FallbackPackageName);
        hashCode = ApplyHashString(hashCode, obj.InternalName);
        if (obj.SteamWorkshopId > 0)
            hashCode ^= (int)obj.SteamWorkshopId;
        

        int ApplyHashString(int currentValue, string str)
        {
            try
            {
                if (str is null || str.Length < 1)
                    return currentValue;
                byte[] b = Encoding.UTF8.GetBytes(str);
                for (int i = 0; i < Math.Min(24, b.Length-1); i++)
                    currentValue ^= b[i];   
                return currentValue;
            }
            catch
            {
                return currentValue;
            }
        }

        return hashCode;
    }
    
    private static readonly int Seed = new Random().Next(436457, int.MaxValue-900);
}

public record LocalizationResourceInfo : ILocalizationResourceInfo
{
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    public string FallbackPackageName { get; init; }
    public CultureInfo TargetCulture { get; init; }
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<string> FilePaths { get; init; }
    public ImmutableArray<CultureInfo> SupportedCultures { get; init; }
    public ImmutableArray<IPackageDependencyInfo> Dependencies { get; init; }
    public bool Optional { get; init; }
}

public readonly struct LuaScriptScriptResourceInfo : ILuaScriptResourceInfo
{
    public ContentPackage OwnerPackage { get; init; }
    public string FallbackPackageName { get; init; }
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
