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
using FluentResults;
using FluentResults.LuaCs;
using OneOf;

namespace Barotrauma.LuaCs.Services;

public partial class PackageService : IPackageService
{
    private readonly ReaderWriterLockSlim _operationsUsageLock = new();
    // only stops race conditions for pointer access
    
    
    // mod config / package scanners/parsers
    private readonly Lazy<IModConfigCreatorService> _configParserService;
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
    private int _isEnabledInModList;

    public bool ConfigsLoaded
    {
        get => ModUtils.Threading.GetBool(ref _configsLoaded);
        private set => ModUtils.Threading.SetBool(ref _configsLoaded, value);
    }
    public bool LocalizationsLoaded
    {
        get => ModUtils.Threading.GetBool(ref _localizationsLoaded);
        private set => ModUtils.Threading.SetBool(ref _localizationsLoaded, value);
    }
    public bool LuaScriptsLoaded
    {
        get => ModUtils.Threading.GetBool(ref _luaScriptsLoaded);
        private set => ModUtils.Threading.SetBool(ref _luaScriptsLoaded, value);
    }
    public bool PluginsLoaded
    {
        get => ModUtils.Threading.GetBool(ref _pluginsLoaded);
        private set => ModUtils.Threading.SetBool(ref _pluginsLoaded, value);
    }
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
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

    public bool IsEnabledInModList
    {
        get => ModUtils.Threading.GetBool(ref _isEnabledInModList);
        private set => ModUtils.Threading.SetBool(ref _isEnabledInModList, value);
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

    public FluentResults.Result LoadResourcesInfo(LoadablePackage cpackage)
    {
        if (cpackage.Package == null)
        {
            return FluentResults.Result.Fail(new Error($"{nameof(LoadResourcesInfo)}: Package is null!")
                .WithMetadata(MetadataType.ExceptionObject,this)
                .WithMetadata(MetadataType.RootObject, cpackage));
        }
        ContentPackage package = cpackage.Package;
        
        _operationsUsageLock.EnterWriteLock();
        LoadingOperationsRunning = true;
        try
        {
            if (IsDisposed)
            {
                return FluentResults.Result.Fail(
                    new Error("Service is disposed.")
                        .WithMetadata(MetadataType.ExceptionObject, this)
                        .WithMetadata(MetadataType.RootObject, package));
            }

            var res = _configParserService.Value.BuildConfigForPackage(package);

            if (res.IsFailed)
            {
                return FluentResults.Result.Fail(res.Errors)
                    .WithError(new Error("PackageService failed to load ModConfigInfo")
                        .WithMetadata(MetadataType.ExceptionObject, _configParserService)
                        .WithMetadata(MetadataType.RootObject, package));
            }

            this.ModConfigInfo = res.Value;
            this.IsEnabledInModList = cpackage.IsEnabled;
            return FluentResults.Result.Ok();
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(new Error(e.Message)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, package)
                .WithMetadata(MetadataType.StackTrace, e.StackTrace));
        }
        finally
        {
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitWriteLock();
        }
    }

    public FluentResults.Result LoadPlugins([NotNull]IAssembliesResourcesInfo assembliesInfo, bool ignoreDependencySorting = false)
    {
        _operationsUsageLock.EnterReadLock();
        LoadingOperationsRunning = true;
        try
        {
            if (CheckResourceSanitation(OneOf<IAssembliesResourcesInfo, ILocalizationsResourcesInfo, 
                    IConfigsResourcesInfo, IConfigProfilesResourcesInfo, ILuaScriptsResourcesInfo>
                    .FromT0(assembliesInfo)) is { IsFailed: true } failed)
            {
                return failed;
            }
            
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
            if (_pluginService.Value.LoadAndInstanceTypes<IAssemblyPlugin>(resources, true, out var instancedTypes) is { IsFailed: true} failed2)
            {
                return failed2.WithError(new Error($"{nameof(LoadPlugins)}: Failed to load plugins for {this.Package.Name}")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, assembliesInfo));
            }

            PluginsLoaded = true;
            return FluentResults.Result.Ok();
        }
        finally
        {
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitReadLock();
        }
    }

    public FluentResults.Result LoadLocalizations([NotNull]ILocalizationsResourcesInfo localizationsInfo)
    {
        _operationsUsageLock.EnterReadLock();
        LoadingOperationsRunning = true;
        try
        {
            if (CheckResourceSanitation(OneOf<IAssembliesResourcesInfo, ILocalizationsResourcesInfo, 
                        IConfigsResourcesInfo, IConfigProfilesResourcesInfo, ILuaScriptsResourcesInfo>
                    .FromT1(localizationsInfo)) is { IsFailed: true } failed)
            {
                return failed;
            }
            
            if (_localizationService.Value.LoadLocalizations(localizationsInfo.Localizations) is { IsFailed: true} failed2)
            {
                return failed2.WithError(new Error($"{nameof(LoadLocalizations)}: Failed to load localizations")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, localizationsInfo));
            }

            LocalizationsLoaded = true;
            return FluentResults.Result.Ok();
        }
        finally
        {
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitReadLock();
        }
    }

    public FluentResults.Result AddLuaScripts([NotNull]ILuaScriptsResourcesInfo luaScriptsInfo)
    {
        _operationsUsageLock.EnterReadLock();
        LoadingOperationsRunning = true;
        try
        {
            if (CheckResourceSanitation(OneOf<IAssembliesResourcesInfo, ILocalizationsResourcesInfo, 
                        IConfigsResourcesInfo, IConfigProfilesResourcesInfo, ILuaScriptsResourcesInfo>
                    .FromT4(luaScriptsInfo)) is { IsFailed: true } failed)
            {
                return failed;
            }
            
            if (_luaScriptService.Value.AddScriptFiles(luaScriptsInfo.LuaScripts) is { IsFailed: true} failed2)
            {
                return failed2.WithError(new Error($"{nameof(LoadLocalizations)}: Failed to load lua scripts.")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, luaScriptsInfo));
            }

            LuaScriptsLoaded = true;
            return FluentResults.Result.Ok();
        }
        finally
        {
            LoadingOperationsRunning = false;
            _operationsUsageLock.ExitReadLock();
        }
    }

    public FluentResults.Result LoadConfig(
        [NotNull]IConfigsResourcesInfo configsResourcesInfo, 
        [NotNull]IConfigProfilesResourcesInfo configProfilesResourcesInfo)
    {
        _operationsUsageLock.EnterReadLock();
        LoadingOperationsRunning = true;
        try
        {
            // register configs
            if (CheckResourceSanitation(OneOf<IAssembliesResourcesInfo, ILocalizationsResourcesInfo, 
                        IConfigsResourcesInfo, IConfigProfilesResourcesInfo, ILuaScriptsResourcesInfo>
                    .FromT2(configsResourcesInfo)) is { IsFailed: true } failed)
            {
                return failed;
            }
            
            if (_configService.Value.AddConfigs(configsResourcesInfo.Configs) is { IsFailed: true} failed2)
            {
                return failed2.WithError(new Error($"{nameof(LoadLocalizations)}: Failed to load configs.")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, configsResourcesInfo));
            }
            
            // register config profiles
            if (CheckResourceSanitation(OneOf<IAssembliesResourcesInfo, ILocalizationsResourcesInfo, 
                        IConfigsResourcesInfo, IConfigProfilesResourcesInfo, ILuaScriptsResourcesInfo>
                    .FromT3(configProfilesResourcesInfo)) is { IsFailed: true } failed3)
            {
                return failed3;
            }
            
            if (_configService.Value.AddConfigsProfiles(configProfilesResourcesInfo.ConfigProfiles) is { IsFailed: true} failed4)
            {
                return failed4.WithError(new Error($"{nameof(LoadLocalizations)}: Failed to load config profiles.")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, configProfilesResourcesInfo));
            }
            
            ConfigsLoaded = true;
            return FluentResults.Result.Ok();
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

    public FluentResults.Result Reset()
    {
        _operationsUsageLock.EnterWriteLock();
        
        try
        {
            if (this.Package is null)
            {
                return FluentResults.Result.Fail(new Error($"Package Service: cannot Dispose of service as ContentPackage and info is not set!")
                    .WithMetadata(MetadataType.ExceptionDetails, nameof(Reset))
                    .WithMetadata(MetadataType.ExceptionObject, this));
            }

            if (this.ModConfigInfo is null)
            {
                return FluentResults.Result.Fail(new Error($"Package Service: cannot Dispose of service as ModConfigInfo is not set!")
                    .WithMetadata(MetadataType.ExceptionDetails, nameof(Reset))
                    .WithMetadata(MetadataType.ExceptionObject, this));
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
                    _loggerService.LogError($"Package Service: Dispose() grace time-out reached while waiting for other operations. Continuing.");
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
            return FluentResults.Result.Ok();
        }
        finally
        {
            _operationsUsageLock.ExitWriteLock();
        }
    }

    #endregion

    #region INTERNAL

    /// <summary>
    /// [Thread Unsafe] Performs sanitation and null checks on resources and returns the results.
    /// NOTE: Requires that resource locks be set by the caller.
    /// </summary>
    /// <param name="resourcesInfos"></param>
    /// <returns></returns>
    private FluentResults.Result CheckResourceSanitation(
        OneOf.OneOf<IAssembliesResourcesInfo, ILocalizationsResourcesInfo,
            IConfigsResourcesInfo, IConfigProfilesResourcesInfo, ILuaScriptsResourcesInfo> resourcesInfos)
    {
        // execute checks based on known types
        return resourcesInfos.Match<FluentResults.Result>(
            ass => ChecksDispatcher(ass, nameof(ass.Assemblies), nameof(LoadPlugins), 
                ass.Assemblies, this.Assemblies),
            loc => ChecksDispatcher(loc, nameof(loc.Localizations), nameof(LoadLocalizations), 
                loc.Localizations, this.Localizations),
            cfg => ChecksDispatcher(cfg, nameof(cfg.Configs), nameof(LoadConfig), 
                cfg.Configs, this.Configs),
            cfp => ChecksDispatcher(cfp, nameof(cfp.ConfigProfiles), nameof(LoadConfig), 
                cfp.ConfigProfiles, this.ConfigProfiles),
            lua => ChecksDispatcher(lua, nameof(lua.LuaScripts), nameof(AddLuaScripts), 
                lua.LuaScripts, this.LuaScripts));
        
        
        /*
         * Helper functions
         */
        FluentResults.Result ChecksDispatcher<T>(object obj, string resName, string callerName,
            ImmutableArray<T> resList, ImmutableArray<T> compareList) 
            where T : class, IPackageInfo, IResourceInfo, IResourceCultureInfo, IPackageDependenciesInfo
        {
            string errMsg = $"{callerName}: Failed to load {resName}.";
            if (DisposeCheck(obj) is { IsFailed: true } failed)
                return failed;
            if (SanitationChecksCore(obj, resName, callerName) is { IsFailed: true } failed1)
                return failed1.WithError(new Error(errMsg));
            if (SanitationChecksEnumerable(resList, resName, callerName) is { IsFailed: true } failed2)
                return failed2.WithError(new Error(errMsg));
            if (DebugCheck(resList, compareList, resName) is {IsFailed: true} failed3)
                return failed3.WithError(new Error(errMsg));
            return FluentResults.Result.Ok();
        }

        FluentResults.Result DisposeCheck(object obj)
        {
            if (IsDisposed)
            {
                return FluentResults.Result.Fail(new Error($"{nameof(PackageService)}: Tried to load resources when disposed.")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, obj));
            }
            return FluentResults.Result.Ok();
        }

        FluentResults.Result DebugCheck<T>(ImmutableArray<T> resList, ImmutableArray<T> compareList, string resName)
            where T : class, IPackageInfo
        {
#if DEBUG
            Stack<Error> errors = new();
            resList.ForEach(res =>
            {
                if (!compareList.Contains(res))
                {
                    errors.Push(new Error($"Failed to load {resName} for: {this.Package.Name}")
                        .WithMetadata(MetadataType.ExceptionDetails, $"Tries to load {resName} resource {res.InternalName} but it is not from this package!")
                        .WithMetadata(MetadataType.ExceptionObject, this)
                        .WithMetadata(MetadataType.RootObject, res));
                }
            });
            if (errors.Count > 0)
            {
                return FluentResults.Result.Fail(errors).WithError(
                    new Error($"{nameof(LoadPlugins)}: errors in {resName} resources.")
                        .WithMetadata(MetadataType.ExceptionObject, this)
                        .WithMetadata(MetadataType.RootObject, this.Package));
            }
#endif
            return FluentResults.Result.Ok();
        }
    }
    
    private FluentResults.Result SanitationChecksCore(object obj, string resTypeInfoName, string callerName)
    {
        Error e = null;
        
        if (obj is null)
        {
            e = new Error($"{nameof(SanitationChecksCore)}: null checks failed!")
                .WithMetadata(MetadataType.ExceptionDetails, "Object is null!")
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.Sources, new List<string>() { resTypeInfoName, callerName });
        }

        if (this.Package is null)
        {
            e = (e ?? new Error($"{nameof(SanitationChecksCore)}: null checks failed!"))
                .WithMetadata(MetadataType.ExceptionDetails, "The Package is null!")
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.Sources, new List<string>() { resTypeInfoName, callerName });
        }

        return e is null ? FluentResults.Result.Ok() : FluentResults.Result.Fail(e);
    }
    
    private FluentResults.Result SanitationChecksEnumerable<T>(ImmutableArray<T> resourceInfos, string resTypeInfoName, string callerName) where T : IResourceInfo, IResourceCultureInfo, IPackageInfo, IPackageDependenciesInfo
    {
        // Check if list is empty. Nothing more to do.
        if (resourceInfos.IsDefaultOrEmpty)
            return FluentResults.Result.Ok();

        Stack<Error> errors = new();
        
        // Check if all resources in the list are registered to this package, throw if not.
        foreach (var resourceInfo in resourceInfos)
        {
            // ownership checks
            if (resourceInfo.OwnerPackage is null)
            { 
                errors.Push(new Error($"Error for resource: {resTypeInfoName}. OwnerPackage is null!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, resourceInfo));
                continue;
            }

            if (resourceInfo.OwnerPackage != this.Package)
            {
                errors.Push(new Error($"Error for resource: {resTypeInfoName}. $\"OwnerPackage {{resourceInfo.OwnerPackage?.Name}} is not the same as this package: {{this.Package}}")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, resourceInfo));
                continue;
            }
            
            if (resourceInfo.Dependencies.IsDefaultOrEmpty)
                continue;

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var pdi in resourceInfo.Dependencies)
            {
                // for clarification: all resources passed to the function should always be loaded.
                // unneeded optional resources should be filtered out before the list is sent.
                // left this as a reminder :)
                /*if (pdi.Optional)
                    return;*/
                if (!_packageManagementService.CheckDependencyLoaded(pdi))
                {
                    errors.Push(new Error($"Dependency missing for resource: {resourceInfo.OwnerPackage.Name}")
                        .WithMetadata(MetadataType.ExceptionDetails, $"Missing dependency: {pdi.DependencyPackage?.Name ?? (pdi.FallbackPackageName.IsNullOrWhiteSpace() ? pdi.SteamWorkshopId.ToString() : pdi.FallbackPackageName)}")
                        .WithMetadata(MetadataType.ExceptionObject, this)
                        .WithMetadata(MetadataType.RootObject, resourceInfo));
                }
            }
            
            // check runtime platform
            if (!_packageManagementService.CheckEnvironmentSupported(resourceInfo))
            {
                errors.Push(new Error($"The resource {resourceInfo.OwnerPackage?.Name} does not support the current platform!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, resourceInfo));
            }
            
            // check local culture
            if (!_localizationService.Value.IsCurrentCultureSupported(resourceInfo))
            {
                errors.Push(new Error($"The resource {resourceInfo.OwnerPackage?.Name} does not support the current culture/region!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, resourceInfo));
            }
        }

        return errors.Count > 0 ? FluentResults.Result.Fail(errors) : FluentResults.Result.Ok();
    }
    
    #endregion
}
