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
    private readonly Lazy<IConfigService> _configService;
    private readonly IPluginManagementService _pluginManagementService;
    private readonly IPackageManagementService _packageManagementService;
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;
    
    // .ctor in server source and client source
    
    private readonly ReaderWriterLockSlim _packageAccessLock = new();
    private ContentPackage _package;
    public ContentPackage Package
    {
        get
        {
            _packageAccessLock.EnterReadLock();
            try
            {
                return _package;
            }
            finally
            {
                _packageAccessLock.ExitReadLock();
            }
        }
        private set
        {
            _packageAccessLock.EnterWriteLock();
            try
            {
                _package = value;
            }
            finally
            {
                _packageAccessLock.ExitWriteLock();
            }
        }
    }

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

    public void LoadPlugins([NotNull]IAssembliesResourcesInfo assembliesInfo, bool ignoreDependencySorting = false)
    {
        _operationsUsageLock.EnterReadLock();
        try
        {
            SanitationChecksCore(assembliesInfo, "assemblies", nameof(LoadPlugins));
            SanitationChecksEnumerable(assembliesInfo.Assemblies, "assemblies", nameof(LoadPlugins));

            // Order these assemblies by internal dependencies
            ImmutableArray<IAssemblyResourceInfo> resources;
            if (ignoreDependencySorting)
            {
                resources = assembliesInfo.Assemblies;
            }
            else // sort by load order
            {
                resources = assembliesInfo.Assemblies
                    .OrderByDescending(a => a.LoadPriority)
                    .ToImmutableArray();
            }
            
            // Try loading them, throw on failure.
            if (!_pluginService.Value.TryLoadAndInstanceTypes<IAssemblyPlugin>(resources, true, out var instancedTypes))
            {
                throw new TypeLoadException($"PackageService: unable to load assemblies for package {this.Package.Name}! Aborting loading!");
            }
        }
        finally
        {
            _operationsUsageLock.ExitReadLock();
        }
    }

    public void LoadLocalizations([NotNull]ILocalizationsResourcesInfo localizationsInfo)
    {
        _operationsUsageLock.EnterReadLock();
        try
        {
            SanitationChecksCore(localizationsInfo, "localizations", nameof(LoadLocalizations));
            SanitationChecksEnumerable(localizationsInfo.Localizations, "localizations", nameof(LoadLocalizations));

            if (!_localizationService.Value.TryLoadLocalizations(localizationsInfo.Localizations))
            {
                throw new FileLoadException($"Package Service: unable to load localizations for package {this.Package.Name}! Aborting!");
            }
        }
        finally
        {
            _operationsUsageLock.ExitReadLock();
        }
    }

    public void LoadLuaScripts([NotNull]ILuaScriptsResourcesInfo luaScriptsInfo)
    {
        _operationsUsageLock.EnterReadLock();
        try
        {
            SanitationChecksCore(luaScriptsInfo, "luaScripts", nameof(LoadLuaScripts));
            SanitationChecksEnumerable(luaScriptsInfo.LuaScripts, "luaScripts", nameof(LoadLuaScripts));

            if (!_luaScriptService.Value.TryAddScriptFiles(luaScriptsInfo.LuaScripts))
            {
                throw new ArgumentException(
                    $"Package Service: unable to add lua files for package {this.Package.Name}! Aborting!");
            }
        }
        finally
        {
            _operationsUsageLock.ExitReadLock();
        }
    }

    public void LoadConfig(
        [NotNull]IConfigsResourcesInfo configsResourcesInfo, 
        [NotNull]IConfigProfilesResourcesInfo configProfilesResourcesInfo)
    {
        _operationsUsageLock.EnterReadLock();
        try
        {
            SanitationChecksCore(configsResourcesInfo, "config", nameof(LoadConfig));
            SanitationChecksCore(configProfilesResourcesInfo, "config profiles", nameof(LoadConfig));
            SanitationChecksEnumerable(configsResourcesInfo.Configs, "config", nameof(LoadConfig));
            SanitationChecksEnumerable(configProfilesResourcesInfo.ConfigProfiles, "config profiles", nameof(LoadConfig));

            if (!_configService.Value.TryAddConfigs(configsResourcesInfo.Configs))
            {
                throw new ArgumentException(
                    $"Package Service: unable to add configs for package {this.Package.Name}! Aborting!");
            }
            
            if (!_configService.Value.TryAddConfigsProfiles(configProfilesResourcesInfo.ConfigProfiles))
            {
                throw new ArgumentException(
                    $"Package Service: unable to add configs profiles for package {this.Package.Name}! Aborting!");
            }
        }
        finally
        {
            _operationsUsageLock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    #endregion

    #region INTERNAL

    private void SanitationChecksCore(object o, string resTypeInfoName, string callerName)
    {
        if (o is null)
        {
            _loggerService.LogError($"Package Service: {resTypeInfoName} resources list is null!");
            throw new NullReferenceException($"Package Service: {resTypeInfoName} resources list is null!");
        }

        if (this.Package is null)
        {
            _loggerService.LogError($"Package Service: package not set at {callerName}()!");
            throw new NullReferenceException($"Package Service: package not set at {callerName}()!");
        }
    }
    
    private void SanitationChecksEnumerable<T>(ImmutableArray<T> resourceInfos, string resTypeInfoName, string callerName) where T : IResourceInfo, IResourceCultureInfo, IPackageInfo, IPackageDependenciesInfo
    {
        // Check if list is empty. Nothing more to do.
        if (resourceInfos.IsDefaultOrEmpty)
            return;

        // Check if all resources in the list are from this package, throw if not.
        foreach (var resourceInfo in resourceInfos)
        {
            // ownership checks
            if (resourceInfo.OwnerPackage is null)
            {
                throw new ArgumentException($"Package Service: {resTypeInfoName} info for resource does not have a package name set! Run by {this.Package.Name}.");
            }

            if (resourceInfo.OwnerPackage != this.Package)
            {
                throw new ArgumentException(
                    $"Package Service: {resTypeInfoName} info does not belong to this package! Owned by {resourceInfo.OwnerPackage.Name} but is run by {this.Package.Name}.");
            }
            
            // Check if external dependencies are loaded and if current environment is supported, throw if not
            if (resourceInfo.Dependencies.IsDefaultOrEmpty)
                continue;
            
            bool resourceMissing = false;
            
            resourceInfo.Dependencies.ForEach(pdi =>
            {
                // for clarification: assemblies passed to the function should always be loaded.
                // optional assemblies should be filtered out before the list is sent.
                // left this as a reminder :)
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
                throw new FileLoadException($"Package Service: dependencies for package {resourceInfo.OwnerPackage.Name} are not loaded.");
            }
            
            // check runtime platform
            if (!_packageManagementService.CheckEnvironmentSupported(resourceInfo))
            {
                throw new PlatformNotSupportedException($"Package service: the {resTypeInfoName} from {resourceInfo.OwnerPackage.Name} is not supported on this platform.");
            }
            
            // check local culture
            if (!_localizationService.Value.IsCurrentCultureSupported(resourceInfo))
            {
                throw new PlatformNotSupportedException($"Package service: the {resTypeInfoName} from {resourceInfo.OwnerPackage.Name} is not supported in this culture.");
            }
        }
    }

    #endregion
}
