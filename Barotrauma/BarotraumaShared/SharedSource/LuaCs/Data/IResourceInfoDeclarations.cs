using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IConfigResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, IPackageInfo { }
public interface IConfigProfileResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, IPackageInfo { }
public interface ILocalizationResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, IPackageInfo { }
/// <summary>
/// Represents loadable Lua files.
/// </summary>
public interface ILuaResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, ILoadableResourceInfo, IPackageInfo { }
public interface IAssemblyResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, ILoadableResourceInfo, IPackageInfo
{
    /// <summary>
    /// The friendly name of the assembly. Script files belonging to the same assembly should all have the same name.
    /// Legacy scripts will all be given the sanitized name of the Content Package they belong to.
    /// </summary>
    public string FriendlyName { get; }
    /// <summary>
    /// Is this entry referring to a script file collection.
    /// </summary>
    public bool IsScript { get; }
}


#region Collections

public interface IAssembliesResourcesInfo
{
    ImmutableArray<IAssemblyResourceInfo> Assemblies { get; }
}

public interface ILocalizationsResourcesInfo
{
    ImmutableArray<ILocalizationResourceInfo> Localizations { get; }
}

public interface ILuaScriptsResourcesInfo
{
    ImmutableArray<ILuaResourceInfo> LuaScripts { get; }
}

public interface IConfigsResourcesInfo
{
    ImmutableArray<IConfigResourceInfo> Configs { get; }
}

public interface IConfigProfilesResourcesInfo
{
    ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles { get; }
}

#endregion
