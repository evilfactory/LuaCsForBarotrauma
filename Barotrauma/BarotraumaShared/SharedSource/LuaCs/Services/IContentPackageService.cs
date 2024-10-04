using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IContentPackageService : IService, 
    // These allow us the pass the IContentPackageService to anything that needs the data without having to directly reference the member
    IPackageDependenciesInfo, IResourceCultureInfo, IAssembliesResourcesInfo, ILocalizationsResourcesInfo, ILuaScriptsResourcesInfo
{
    ContentPackage Package { get; }
    IModConfigInfo ModConfigInfo { get; }
    /// <summary>
    /// Try to load the XML config and resources information from the given package.
    /// </summary>
    /// <param name="package"></param>
    /// <returns>Whether the package was parsed without errors and any information was found. Will return false for purely vanilla packages.</returns>
    bool TryLoadResourcesInfo(ContentPackage package);

    bool TryLoadAssemblies();
    bool TryLoadLocalizations();
    bool TryLoadLuaScripts();
    bool TryLoadStyles();
    bool TryLoadConfig();
}

