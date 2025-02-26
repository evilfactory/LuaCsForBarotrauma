using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Barotrauma.LuaCs.Data;

public interface IConfigResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, IDataInfo { }
public interface IConfigProfileResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, IDataInfo { }
public interface ILocalizationResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, IDataInfo { }

/// <summary>
/// Represents loadable Lua files.
/// </summary>
public interface ILuaScriptResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, IDataInfo
{
    /// <summary>
    /// Should this script be run automatically.
    /// </summary>
    public bool IsAutorun { get; }
}

public interface IAssemblyResourceInfo : IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo, IDataInfo
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
    ImmutableArray<ILuaScriptResourceInfo> LuaScripts { get; }
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
