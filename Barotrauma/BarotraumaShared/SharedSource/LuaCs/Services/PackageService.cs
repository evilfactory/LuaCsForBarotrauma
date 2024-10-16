using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;
using Barotrauma.Steam;
using QuikGraph;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService : IContentPackageService
{
    private readonly ReaderWriterLockSlim _operationsUsageLock = new();
    // only stops race conditions for pointer access
    private readonly ReaderWriterLockSlim _modConfigUsageLock = new();
    
    // mod config / package scanners/parsers
    private readonly Lazy<IXmlModConfigConverterService> _modConfigConverterService;
    private readonly Lazy<ILegacyConfigService> _legacyConfigService;
    private readonly Lazy<ILuaScriptService> _luaScriptService;
    private readonly Lazy<ILocalizationService> _localizationService;
    private readonly Lazy<IPluginService> _pluginService;
    private readonly IPluginManagementService _pluginManagementService;
    private readonly IPackageManagementService _packageManagementService;
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;
    
    // .ctor in server source and client source
    
    public ContentPackage Package { get; private set; }

    #region DataContracts

    private IModConfigInfo _modConfigInfo;
    public IModConfigInfo ModConfigInfo
    {
        get
        {
            _modConfigUsageLock.EnterReadLock();
            try
            {
                return _modConfigInfo;
            }
            finally
            {
                _modConfigUsageLock.ExitReadLock();
            }
        }
        private set
        {
            _modConfigUsageLock.EnterWriteLock();
            try
            {
                _modConfigInfo = value;
            }
            finally
            {
                _modConfigUsageLock.ExitWriteLock();
            }
        }
    }
    public ImmutableArray<IPackageDependencyInfo> Dependencies => ModConfigInfo?.Dependencies ?? ImmutableArray<IPackageDependencyInfo>.Empty;
    public ImmutableArray<CultureInfo> SupportedCultures => ModConfigInfo?.SupportedCultures ?? ImmutableArray<CultureInfo>.Empty;
    public ImmutableArray<IAssemblyResourceInfo> Assemblies => ModConfigInfo?.Assemblies ?? ImmutableArray<IAssemblyResourceInfo>.Empty;
    public ImmutableArray<ILocalizationResourceInfo> Localizations => ModConfigInfo?.Localizations ?? ImmutableArray<ILocalizationResourceInfo>.Empty;
    public ImmutableArray<ILuaResourceInfo> LuaScripts => ModConfigInfo?.LuaScripts ?? ImmutableArray<ILuaResourceInfo>.Empty;

    #endregion

    #region PublicAPI

    public bool TryLoadResourcesInfo(ContentPackage package)
    {
        _operationsUsageLock.EnterUpgradeableReadLock();
        try
        {
            // try loading the ModConfig.xml. If it fails, use the Legacy loader to try and construct one from the package structure.
            if (_storageService.TryLoadPackageXml(package, "ModConfig.xml", out var configXml)
                && configXml.Root is not null)
            {
                if (_modConfigConverterService.Value.TryParseResource(configXml.Root, out IModConfigInfo configInfo))
                {
                    _operationsUsageLock.EnterWriteLock();
                    try
                    {
                        ModConfigInfo = configInfo;
                    }
                    finally
                    {
                        _operationsUsageLock.ExitWriteLock();
                    }
                }
                else
                {
                    _loggerService.LogError(
                        $"Failed to parse ModConfig.xml for package {package.Name}, package mod content not loaded.");
                    return false;
                }
            }
            else if (_legacyConfigService.Value.TryBuildModConfigFromLegacy(package, out var legacyConfig))
            {
                _operationsUsageLock.EnterWriteLock();
                try
                {
                    ModConfigInfo = legacyConfig;
                }
                finally
                {
                    _operationsUsageLock.ExitWriteLock();
                }
            }
            else
            {
                // vanilla mod or broken
                return false;
            }

            
            return true;
        }
        finally
        {
            _operationsUsageLock.ExitUpgradeableReadLock();
        }
    }

    public bool TryLoadPlugins([NotNull] IAssembliesResourcesInfo assembliesInfo, bool ignoreDependencySorting = false)
    {
        _operationsUsageLock.EnterReadLock();
        try
        {
            if (assembliesInfo is null)
            {
                _loggerService.LogError($"Package Service: assemblies resources list is null!");
                throw new NullReferenceException($"Package Service: assemblies resources list is null!");
            }

            if (this.Package is null)
            {
                _loggerService.LogError($"Package Service: package not set at TryLoadPlugins()!");
                throw new NullReferenceException($"Package Service: package not set at TryLoadPlugins()!");
            }

            // Check if assemblies list is empty. Return false if it is.
            if (assembliesInfo.Assemblies.IsDefaultOrEmpty)
                return false;

            // Check if all assembly resources in the list are from this package, throw if not.
            foreach (var resourceInfo in assembliesInfo.Assemblies)
            {
                if (resourceInfo.OwnerPackage is null)
                {
                    _loggerService.LogError(
                        $"Package Service: assembly info for assembly {resourceInfo.InternalName} does not have a package name set! Run by {this.Package.Name}.");
                    throw new ArgumentException(
                        $"Package Service: assembly info for assembly {resourceInfo.InternalName} does not have a package name set! Run by {this.Package.Name}.");
                }

                if (resourceInfo.OwnerPackage != this.Package)
                {
                    _loggerService.LogError(
                        $"Package Service: assembly info for assembly {resourceInfo.InternalName} does not belong to this package! Owned by {resourceInfo.OwnerPackage.Name} but is run by {this.Package.Name}.");
                    throw new ArgumentException(
                        $"Package Service: assembly info for assembly {resourceInfo.InternalName} does not belong to this package! Owned by {resourceInfo.OwnerPackage.Name} but is run by {this.Package.Name}.");
                }
            }

            // Check if external dependencies are loaded and if current environment is supported, throw if not.
            foreach (var resourceInfo in assembliesInfo.Assemblies)
            {
                if (resourceInfo.Dependencies.IsDefaultOrEmpty)
                    continue;
                bool resourceMissing = false;
                
                resourceInfo.Dependencies.ForEach(pdi =>
                {
                    // for clarification: assemblies passed to the function should always be loaded.
                    // optional assemblies should be filtered out before the list is sent.
                    /*if (pdi.Optional)
                        return;*/
                    if (!_packageManagementService.CheckDependencyLoaded(pdi))
                    {
                        resourceMissing = true;
                        _loggerService.LogError(
                            $"Package Service: the following dependency for package {resourceInfo.OwnerPackage.Name} is not loaded: {pdi.DependencyPackage?.Name ?? (pdi.PackageName.IsNullOrWhiteSpace() ? pdi.SteamWorkshopId.ToString() : pdi.PackageName)}");
                    }
                });

                if (!resourceMissing)
                {
                    throw new FileLoadException(
                        $"Package Service: dependencies for package {resourceInfo.OwnerPackage.Name} are not loaded");
                }
                
                // check environment
                if (!_packageManagementService.CheckEnvironmentSupported(resourceInfo, resourceInfo))
                {
                    throw new PlatformNotSupportedException(
                        $"Package service: the assembly {resourceInfo.InternalName} is not supported on this platform.");
                }
            }

            // Order these assemblies by internal dependencies
            ImmutableArray<IAssemblyResourceInfo> resources;
            if (ignoreDependencySorting)
            {
                resources = assembliesInfo.Assemblies;
            }
            else
            {
                Dictionary<Guid, IAssemblyResourceInfo> resourceGuids = new();
                var graph = new AdjacencyGraph<Guid, Edge<Guid>>();

                foreach (var assemblyInfo in assembliesInfo.Assemblies) 
                {
                    resourceGuids.Add(Guid.NewGuid(), assemblyInfo);
                }

                // build a graph edge list, include only intramod dependencies.
                foreach (var resourceGuid in resourceGuids)
                {
                    
                }
                
            }
            
            // Try loading them, return success states.


            throw new NotImplementedException();
        }
        finally
        {
            _operationsUsageLock.ExitReadLock();
        }
    }

    public bool TryLoadLocalizations(ILocalizationsResourcesInfo localizationsInfo)
    {
        throw new NotImplementedException();
    }

    public bool TryLoadLuaScripts(ILuaScriptsResourcesInfo luaScriptsInfo)
    {
        throw new NotImplementedException();
    }

    public bool TryLoadConfig()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    #endregion


    #region Internal

    private bool AreContentsFromPackage(IEnumerable<IPackageInfo> packages)
    {
        return packages is null || packages.All(package => package.OwnerPackage is not null && package.OwnerPackage == this.Package);
    }

    #endregion

    
}
