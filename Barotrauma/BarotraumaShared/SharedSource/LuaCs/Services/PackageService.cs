using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

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

    public bool TryLoadPlugins(IAssembliesResourcesInfo assembliesInfo = null)
    {
        _operationsUsageLock.EnterReadLock();
        try
        {
            if (Package is null)
            {
                _loggerService.LogError($"PackageService: tried to load plugins without a package being set!");
                return false;
            }

            if (!assembliesInfo?.Assemblies.IsDefaultOrEmpty ?? false)
            {
                // never load another package's assemblies in this service
                if (!AreContentsFromPackage(assembliesInfo.Assemblies))
                {
                    // log it then throw
                    _loggerService.LogError(
                        $"Package Service: tried to load assemblies for an unrelated package! Package service current: {this.Package}.");
                    throw new ArgumentException(
                        $"Package Service: tried to load assemblies for an unrelated package! Package service current: {this.Package}.");
                }
                
                foreach (var assemblyInfo in assembliesInfo.Assemblies)
                {
                    // All assemblies must have an internal name
                    if (assemblyInfo.InternalName.IsNullOrWhiteSpace())
                    {
                        _loggerService.LogError(
                            $"Package Service: assembly info in package {assemblyInfo.OwnerPackage.Name} does not have an InternalName (see ModConfig.xml). Cannot continue.");
                        throw new ArgumentException(
                            $"Package Service: tried to load assemblies for an unrelated package! Owner package {assemblyInfo.OwnerPackage?.Name}, package service current: {this.Package}.");
                    }

#if DEBUG
                    // Something has already loaded this assembly. This shouldn't stop execution since there's no isolation but this may be a package issue for devs,
                    // or it could just be a common library that multiple content packages contain.
                    if (_pluginService.Value.IsAssemblyLoadedGlobal(assemblyInfo.InternalName))
                    {
                        _loggerService.LogDebugWarning($"Package Service: The assembly {assemblyInfo.InternalName} is already loaded.");
                    }
#endif
                }
            }
            else
            {
                // either the list is empty or was null, so we load the default set
                assembliesInfo = this;
                if (assembliesInfo.Assemblies.IsDefaultOrEmpty)
                {
                    // nothing to load
                    return true;
                }
            }

            // what we need to filter to load
            // must have dependencies already loaded if not in the same package, throw otherwise
            // should we request that they be loaded on-demand instead?
            // check platform, target and culture supported.
            // must not be lazy loadable (unless required by another package to be loaded)
            // if marked as optional, load if dependent packages are loaded, do not throw

            foreach (IAssemblyResourceInfo resourceInfo in assembliesInfo.Assemblies)
            {
                if (resourceInfo.Dependencies.IsDefaultOrEmpty)
                    continue;
                if (!_packageManagementService.CheckDependenciesLoaded(resourceInfo.Dependencies, out var missingPackages) && !resourceInfo.LazyLoad)
                {
                    _loggerService.LogError($"Package Service: not all dependencies for package {resourceInfo.OwnerPackage?.Name} are loaded. Missing Packages:");
                    if (missingPackages.Any())
                    {
                        missingPackages.ForEach(p => _loggerService.LogError($">> SteamID: {p.SteamWorkshopId} | Name: {p.PackageName}"));
                    }
                    throw new MissingContentPackageException(this.Package, $"Package Service: Missing dependency packages for package {resourceInfo.OwnerPackage?.Name}");
                }
            }
            
            

            


            throw new NotImplementedException();
        }
        finally
        {
            _operationsUsageLock.ExitReadLock();
        }
    }

    public bool TryLoadLocalizations()
    {
        throw new NotImplementedException();
    }

    public bool TryLoadLuaScripts()
    {
        throw new NotImplementedException();
    }

    public partial bool TryLoadStyles();

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
