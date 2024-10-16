using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    bool TryLoadResourcesInfo([NotNull]ContentPackage package);
    bool TryLoadPlugins([NotNull]IAssembliesResourcesInfo assembliesInfo, bool ignoreDependencySorting = false);
    bool TryLoadLocalizations([NotNull]ILocalizationsResourcesInfo localizationsInfo);
    bool TryLoadLuaScripts([NotNull]ILuaScriptsResourcesInfo luaScriptsInfo);
#if CLIENT
    bool TryLoadStyles([NotNull]IStylesResourcesInfo stylesInfo);
#endif
    bool TryLoadConfig();
}

