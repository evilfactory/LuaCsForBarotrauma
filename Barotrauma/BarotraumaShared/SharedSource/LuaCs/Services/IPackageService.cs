using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IPackageService : IService, 
    // These allow us the pass the IContentPackageService to anything that needs the data without having to directly reference the member
    IResourceCultureInfo, IAssembliesResourcesInfo, ILocalizationsResourcesInfo, ILuaScriptsResourcesInfo
{
    ContentPackage Package { get; }
    IModConfigInfo ModConfigInfo { get; }
    /// <summary>
    /// Try to load the XML config and resources information from the given package.
    /// </summary>
    /// <param name="package"></param>
    /// <returns>Whether the package was parsed without errors and any information was found. Will return false for purely vanilla packages.</returns>
    bool TryLoadResourcesInfo([NotNull]ContentPackage package);
    /// <summary>
    /// Tries to load all assemblies and instance plugins for the given resources list, regardless whether they're marked as optional and/or lazy load.
    /// Will sort by load priority unless overriden/bypassed.
    /// </summary>
    /// <param name="assembliesInfo"></param>
    /// <param name="ignoreDependencySorting"></param>
    /// <returns>Whether loading is successful. Returns true on an empty list.</returns>
    void LoadPlugins([NotNull]IAssembliesResourcesInfo assembliesInfo, bool ignoreDependencySorting = false);
    void LoadLocalizations([NotNull]ILocalizationsResourcesInfo localizationsInfo);
    void AddLuaScripts([NotNull]ILuaScriptsResourcesInfo luaScriptsInfo);
#if CLIENT
    void LoadStyles([NotNull]IStylesResourcesInfo stylesInfo);
#endif
    void LoadConfig([NotNull]IConfigsResourcesInfo configsResourcesInfo, [NotNull]IConfigProfilesResourcesInfo configProfilesResourcesInfo);
}

