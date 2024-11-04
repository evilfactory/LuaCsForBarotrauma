using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public interface IPackageService : IReusableService, 
    // These allow us the pass the IContentPackageService to anything that needs the data without having to directly reference the member
    IResourceCultureInfo, IAssembliesResourcesInfo, ILocalizationsResourcesInfo, ILuaScriptsResourcesInfo
{
    ContentPackage Package { get; }
    IModConfigInfo ModConfigInfo { get; }
    bool IsEnabledInModList { get; }
    /// <summary>
    /// Try to load the XML config and resources information from the given package.
    /// </summary>
    /// <param name="package"></param>
    /// <returns>Whether the package was parsed without errors.</returns>
    FluentResults.Result LoadResourcesInfo([NotNull]LoadablePackage package);
    /// <summary>
    /// Tries to load all assemblies and instance plugins for the given resources list, regardless whether they're marked as optional and/or lazy load.
    /// Will sort by load priority unless overriden/bypassed.
    /// </summary>
    /// <param name="assembliesInfo"></param>
    /// <param name="ignoreDependencySorting"></param>
    /// <returns>Whether loading is successful. Returns true on an empty list.</returns>
    FluentResults.Result LoadPlugins([NotNull]IAssembliesResourcesInfo assembliesInfo, bool ignoreDependencySorting = false);
    FluentResults.Result LoadLocalizations([NotNull]ILocalizationsResourcesInfo localizationsInfo);
    FluentResults.Result AddLuaScripts([NotNull]ILuaScriptsResourcesInfo luaScriptsInfo);
#if CLIENT
    FluentResults.Result LoadStyles([NotNull]IStylesResourcesInfo stylesInfo);
#endif
    FluentResults.Result LoadConfig([NotNull]IConfigsResourcesInfo configsResourcesInfo, [NotNull]IConfigProfilesResourcesInfo configProfilesResourcesInfo);
}

