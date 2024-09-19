using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService : IPackageService
{
    private readonly ReaderWriterLockSlim _operationsUsageLock = new();
    // only stops race conditions for pointer access
    
    
    // mod config / package scanners/parsers
    private readonly Lazy<IXmlModConfigConverterService> _modConfigConverterService;
    private readonly Lazy<ILegacyConfigService> _legacyConfigService;
    private readonly Lazy<ILuaScriptService> _luaScriptService;
    private readonly Lazy<ILocalizationService> _localizationService;
    private readonly Lazy<IPluginService> _pluginService;
    private readonly Lazy<IConfigService> _configService;
    private readonly IPackageManagementService _packageManagementService;
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;
    
    // .ctor in server source and client source
    
    // state monitors
    private int _configsLoaded, _localizationsLoaded, _luaScriptsLoaded, _pluginsLoaded, _isDisposed;
    private int _loadingOperationsRunning;

    public bool ConfigsLoaded
    {
        get => GetThreadSafeBool(ref _configsLoaded);
        private set => SetThreadSafeBool(ref _configsLoaded, value);
    }
    public bool LocalizationsLoaded
    {
        get => GetThreadSafeBool(ref _localizationsLoaded);
        private set => SetThreadSafeBool(ref _localizationsLoaded, value);
    }
    public bool LuaScriptsLoaded
    {
        get => GetThreadSafeBool(ref _luaScriptsLoaded);
        private set => SetThreadSafeBool(ref _luaScriptsLoaded, value);
    }
    public bool PluginsLoaded
    {
        get => GetThreadSafeBool(ref _pluginsLoaded);
        private set => SetThreadSafeBool(ref _pluginsLoaded, value);
    }
    public bool IsDisposed
    {
        get => GetThreadSafeBool(ref _isDisposed);
        private set => SetThreadSafeBool(ref _isDisposed, value);
    }

    private bool LoadingOperationsRunning
    {
        get => Interlocked.CompareExchange(ref _loadingOperationsRunning, 0, 0) > 0;
        set // we use the set as our inc/decr 
        {
            if (value)
            {
                Interlocked.Add(ref _loadingOperationsRunning, 1);
            }
            else
            {
                Interlocked.Add(ref _loadingOperationsRunning, -1);
            }
        }
    }
    
    #region Member: ContentPackage

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

    #endregion

    #region DataContracts

    #region Member: ModConfigInfo

    private readonly ReaderWriterLockSlim _modConfigUsageLock = new();
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

    #endregion
    
    public ImmutableArray<CultureInfo> SupportedCultures => ModConfigInfo?.SupportedCultures ?? ImmutableArray<CultureInfo>.Empty;
    public ImmutableArray<IAssemblyResourceInfo> Assemblies => ModConfigInfo?.Assemblies ?? ImmutableArray<IAssemblyResourceInfo>.Empty;
    public ImmutableArray<ILocalizationResourceInfo> Localizations => ModConfigInfo?.Localizations ?? ImmutableArray<ILocalizationResourceInfo>.Empty;
    public ImmutableArray<ILuaResourceInfo> LuaScripts => ModConfigInfo?.LuaScripts ?? ImmutableArray<ILuaResourceInfo>.Empty;
    public ImmutableArray<IConfigResourceInfo> Configs => ModConfigInfo?.Configs ?? ImmutableArray<IConfigResourceInfo>.Empty;
    public ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles => ModConfigInfo?.ConfigProfiles ?? ImmutableArray<IConfigProfileResourceInfo>.Empty;

    #endregion

    #region PublicAPI

    public bool TryLoadResourcesInfo(ContentPackage package)
    {
        _operationsUsageLock.EnterWriteLock();
        LoadingOperationsRunning = true;
        try
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException($"This package service instance is disposed!");
            }

            // try loading the ModConfig.xml. If it fails, use the Legacy loader to try and construct one from the package structure.
            if (_storageService.TryLoadPackageXml(package, "ModConfig.xml", out var configXml)
                && configXml.Root is not null)
            {
                if (_modConfigConverterService.Value.TryParseResource(configXml.Root, out IModConfigInfo configInfo))
                {
                    ModConfigInfo = configInfo;
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
                ModConfigInfo = legacyConfig;
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
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitWriteLock();
        }
    }

    public void LoadPlugins([NotNull]IAssembliesResourcesInfo assembliesInfo, bool ignoreDependencySorting = false)
    {
        _operationsUsageLock.EnterReadLock();
        LoadingOperationsRunning = true;
        try
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException($"This package service instance is disposed!");
            }
            
            SanitationChecksCore(assembliesInfo, "assemblies", nameof(LoadPlugins));
            SanitationChecksEnumerable(assembliesInfo.Assemblies, "assemblies", nameof(LoadPlugins));

#if DEBUG
            assembliesInfo.Assemblies.ForEach(ari =>
            {
                if (!this.Assemblies.Contains(ari))
                {
                    throw new ArgumentException(
                        $"Package Service: tried to load the assembly resource {ari.InternalName} for package {this.Package.Name} but it is not in the list for this package.");
                }
            });      
#endif
            
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

            PluginsLoaded = true;
        }
        finally
        {
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitReadLock();
        }
    }

    public void LoadLocalizations([NotNull]ILocalizationsResourcesInfo localizationsInfo)
    {
        _operationsUsageLock.EnterReadLock();
        LoadingOperationsRunning = true;
        try
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException($"This package service instance is disposed!");
            }
            
            SanitationChecksCore(localizationsInfo, "localizations", nameof(LoadLocalizations));
            SanitationChecksEnumerable(localizationsInfo.Localizations, "localizations", nameof(LoadLocalizations));

#if DEBUG
            localizationsInfo.Localizations.ForEach(ri =>
            {
                if (!this.Localizations.Contains(ri))
                {
                    throw new ArgumentException(
                        $"Package Service: tried to load the localization resource for package {this.Package.Name} but it is not in the list for this package.");
                }
            });      
#endif
            
            if (!_localizationService.Value.TryLoadLocalizations(localizationsInfo.Localizations))
            {
                throw new FileLoadException($"Package Service: unable to load localizations for package {this.Package.Name}! Aborting!");
            }

            LocalizationsLoaded = true;
        }
        finally
        {
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitReadLock();
        }
    }

    public void AddLuaScripts([NotNull]ILuaScriptsResourcesInfo luaScriptsInfo)
    {
        _operationsUsageLock.EnterReadLock();
        LoadingOperationsRunning = true;
        try
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException($"This package service instance is disposed!");
            }
            
            SanitationChecksCore(luaScriptsInfo, "luaScripts", nameof(AddLuaScripts));
            SanitationChecksEnumerable(luaScriptsInfo.LuaScripts, "luaScripts", nameof(AddLuaScripts));

#if DEBUG
            luaScriptsInfo.LuaScripts.ForEach(ri =>
            {
                if (!this.LuaScripts.Contains(ri))
                {
                    throw new ArgumentException(
                        $"Package Service: tried to load the lua script resource for package {this.Package.Name} but it is not in the list for this package.");
                }
            });      
#endif
            
            if (!_luaScriptService.Value.TryAddScriptFiles(luaScriptsInfo.LuaScripts))
            {
                throw new ArgumentException(
                    $"Package Service: unable to add lua files for package {this.Package.Name}! Aborting!");
            }

            LuaScriptsLoaded = true;
        }
        finally
        {
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitReadLock();
        }
    }

    public void LoadConfig(
        [NotNull]IConfigsResourcesInfo configsResourcesInfo, 
        [NotNull]IConfigProfilesResourcesInfo configProfilesResourcesInfo)
    {
        _operationsUsageLock.EnterReadLock();
        LoadingOperationsRunning = true;
        try
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException($"This package service instance is disposed!");
            }
            
            SanitationChecksCore(configsResourcesInfo, "config", nameof(LoadConfig));
            SanitationChecksCore(configProfilesResourcesInfo, "config profiles", nameof(LoadConfig));
            SanitationChecksEnumerable(configsResourcesInfo.Configs, "config", nameof(LoadConfig));
            SanitationChecksEnumerable(configProfilesResourcesInfo.ConfigProfiles, "config profiles", nameof(LoadConfig));

#if DEBUG
            configsResourcesInfo.Configs.ForEach(ri =>
            {
                if (!this.Configs.Contains(ri))
                {
                    throw new ArgumentException(
                        $"Package Service: tried to load the configs resource for package {this.Package.Name} but it is not in the list for this package.");
                }
            }); 
            
            configProfilesResourcesInfo.ConfigProfiles.ForEach(ri =>
            {
                if (!this.ConfigProfiles.Contains(ri))
                {
                    throw new ArgumentException(
                        $"Package Service: tried to load the localization resource for package {this.Package.Name} but it is not in the list for this package.");
                }
            }); 
#endif
            
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
            ConfigsLoaded = true;
        }
        finally
        {
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        /*
         * Notes: we need to unload this package from services in the order that the services are dependent on each other.
         * Unloading Order: Lua Scripts > Assemblies > Config Profiles > Configs > Styles > Localizations
         */
        _operationsUsageLock.EnterWriteLock();
        try
        {
            if (this.Package is null)
            {
                _loggerService.LogError(
                    $"Package Service: cannot Dispose of service as ContentPackage and info is not set!");
                return;
            }

            if (this.ModConfigInfo is null)
            {
                _loggerService.LogError($"Package Service: cannot Dispose of service as ModConfigInfo is not loaded!");
                return;
            }

            /*
             * To be graceful, we want to ensure that any async calls and other threads are allowed to be processed before we begin
             * disposal to reduce friction with other thread operations, so we release the lock and periodically check it
             * to see of other threads have finished operations before cleaning everything up.
             */

            IsDisposed = true; // set stop flag, callers should handle exception cases
            Interlocked.MemoryBarrier(); //ensure cache states 

            DateTime timeoutLimit = DateTime.Now.AddSeconds(10);
            while (LoadingOperationsRunning)
            {
                _operationsUsageLock.ExitWriteLock();
                Thread.Sleep(1);
                _operationsUsageLock.EnterWriteLock();
                if (timeoutLimit < DateTime.Now)
                {
                    _loggerService.LogError($"Package Service: Dispose() time out reached while waiting for other operations. Continuing.");
                    break;
                }
            }
            
            GC.SuppressFinalize(this);

            _luaScriptService.Value.RemoveScriptFiles(this.LuaScripts);
            _pluginService.Value.DisposePlugins();
            _configService.Value.RemoveConfigsProfiles(this.ConfigProfiles);
            _configService.Value.RemoveConfigs(this.Configs);
#if CLIENT
            _stylesService.Value.UnloadAllStyles();
#endif
            _localizationService.Value.Remove(this.Localizations);

            ModConfigInfo = null;
            Package = null;
        }
        catch
        {
            _loggerService.LogError($"Package Service: exception while running Dispose().");
            throw;
        }
        finally
        {
            _operationsUsageLock.ExitWriteLock();
        }
    }

    public void Reset()
    {
        _operationsUsageLock.EnterWriteLock();
        try
        {
            if (this.Package is null)
            {
                _loggerService.LogError(
                    $"Package Service: cannot Dispose of service as ContentPackage and info is not set!");
                return;
            }

            if (this.ModConfigInfo is null)
            {
                _loggerService.LogError($"Package Service: cannot Dispose of service as ModConfigInfo is not loaded!");
                return;
            }
            
            Interlocked.MemoryBarrier(); //ensure cache states 

            DateTime timeoutLimit = DateTime.Now.AddSeconds(10);
            while (LoadingOperationsRunning)
            {
                _operationsUsageLock.ExitWriteLock();
                Thread.Sleep(1);
                _operationsUsageLock.EnterWriteLock();
                if (timeoutLimit < DateTime.Now)
                {
                    _loggerService.LogError($"Package Service: Dispose() time out reached while waiting for other operations. Continuing.");
                    break;
                }
            }

            if (LuaScriptsLoaded)
            {
                _luaScriptService.Value.RemoveScriptFiles(this.LuaScripts);
                LuaScriptsLoaded = false;
            }

            if (PluginsLoaded)
            {
                _pluginService.Value.DisposePlugins();
                PluginsLoaded = false;
            }

            if (ConfigsLoaded)
            {
                _configService.Value.RemoveConfigsProfiles(this.ConfigProfiles);
                _configService.Value.RemoveConfigs(this.Configs);
                ConfigsLoaded = false;
            }

            if (LocalizationsLoaded)
            {
                _localizationService.Value.Remove(this.Localizations);
                LocalizationsLoaded = false;
            }
        }
        finally
        {
            _operationsUsageLock.ExitWriteLock();
        }
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

        // Check if all resources in the list are registered to this package, throw if not.
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool GetThreadSafeBool(ref int var) => Interlocked.CompareExchange(ref var, 1, 1) == 1;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetThreadSafeBool(ref int var, bool value)
    {
        if (value)
        {
            Interlocked.CompareExchange(ref var, 1, 0);
        }
        else
        {
            Interlocked.CompareExchange(ref var, 0, 1);
        }
    }
    
    #endregion
}
