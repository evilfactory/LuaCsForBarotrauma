using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.Toolkit.Diagnostics;
using MoonSharp.VsCodeDebugger.SDK;

namespace Barotrauma.LuaCs;

public sealed class ModConfigService : IModConfigService
{
    private IStorageService  _storageService;
    private ILoggerService  _logger;
    private IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo> _assemblyParserService;
    private IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo>  _luaScriptParserService;
    private IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>  _configParserService;
    private readonly AsyncReaderWriterLock _operationsLock = new();
    
    public ModConfigService(IStorageService storageService, 
        IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo> assemblyParserService, 
        IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo> luaScriptParserService, 
        IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo> configParserService, 
        ILoggerService logger)
    {
        _storageService = storageService;
        _assemblyParserService = assemblyParserService;
        _luaScriptParserService = luaScriptParserService;
        _configParserService = configParserService;
        _logger = logger;
    } 
    
    #region Dispose

    public void Dispose()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
            return;
        
        try
        {
            _storageService.Dispose();
            _logger.Dispose();
            _assemblyParserService.Dispose();
            _luaScriptParserService.Dispose();
            _configParserService.Dispose();

            _storageService = null;
            _logger = null;
            _assemblyParserService = null;
            _luaScriptParserService = null;
            _configParserService = null;
        }
        catch
        {
            // ignored
        }
    }

    private int _isDisposed = 0;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    #endregion

    public async Task<Result<IModConfigInfo>> CreateConfigAsync(ContentPackage src)
    {
        Guard.IsNotNull(src, nameof(src));
        using var lck = await _operationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        
        if (await TryGetModConfigXmlAsync(src) is { IsSuccess: true, Value: { } config })
        {
            return await CreateFromConfigXmlAsync(src, config);
        }
        
        return await CreateFromLegacyAsync(src);
    }

    public async Task<ImmutableArray<(ContentPackage Source, Result<IModConfigInfo> Config)>> CreateConfigsAsync(ImmutableArray<ContentPackage> src)
    {
        if (src.IsDefaultOrEmpty)
            ThrowHelper.ThrowArgumentNullException($"{nameof(CreateConfigsAsync)}: The supplied array is default or empty!");
        using var lck = await _operationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);

        var builder = ImmutableArray.CreateBuilder<Task<Task<Result<IModConfigInfo>>>>(src.Length);
        foreach (var srcItem in src)
        {
            builder.Add(Task.Factory.StartNew(async Task<Result<IModConfigInfo>> () => await CreateConfigAsync(srcItem)));
        }
        var taskResults = await Task.WhenAll(builder.ToImmutable());
        var returnResults = ImmutableArray.CreateBuilder<(ContentPackage Source, Result<IModConfigInfo> Config)>();
        foreach (var taskResult in taskResults)
        {
            if (taskResult.IsFaulted)
            {
                ThrowHelper.ThrowInvalidOperationException($"{nameof(CreateConfigsAsync)}: Task failed: {taskResult.Exception?.Message}");
            }

            var r = await taskResult;
            returnResults.Add((r.Value.Package, r));
        }

        return returnResults.ToImmutable();
    }

    //--- Helpers
    private async Task<Result<XElement>> TryGetModConfigXmlAsync(ContentPackage src)
    {
        return await _storageService.LoadPackageXmlAsync(ContentPath.FromRaw(src, "%ModDir%/ModConfig.xml")) is { IsSuccess: true, Value: { Root: {} config} } 
            ? FluentResults.Result.Ok(config) 
            : FluentResults.Result.Fail<XElement>("ModConfig.xml not found");
    }

    private async Task<Result<IModConfigInfo>> CreateFromConfigXmlAsync(ContentPackage owner, XElement src)
    {
        var asmTask = Task.Factory.StartNew(async () => await GetAssembliesFromXml(owner, src));
        var cfgTask = Task.Factory.StartNew(async () => await GetConfigsFromXml(owner, src));
        var luaTask = Task.Factory.StartNew(async () => await GetLuaScriptsFromXml(owner, src));
        
        await Task.WhenAll(asmTask, cfgTask, luaTask);

        return FluentResults.Result.Ok<IModConfigInfo>(new ModConfigInfo()
        {
            Package = owner,
            Assemblies = await await asmTask,
            Configs = await await cfgTask,
            LuaScripts = await await luaTask
        });

        async Task<ImmutableArray<ILuaScriptResourceInfo>> GetLuaScriptsFromXml(ContentPackage contentPackage, 
            XElement cfgElement)
        {
            return await GetResourceFromXml<ILuaScriptResourceInfo>(contentPackage, cfgElement, "Lua", "FileGroup", _luaScriptParserService);
        }

        async Task<ImmutableArray<IConfigResourceInfo>> GetConfigsFromXml(ContentPackage contentPackage, 
            XElement cfgElement)
        {
            return await GetResourceFromXml<IConfigResourceInfo>(contentPackage, cfgElement, "Config", "FileGroup", _configParserService);

        }

        async Task<ImmutableArray<IAssemblyResourceInfo>> GetAssembliesFromXml(ContentPackage contentPackage, 
            XElement cfgElement)
        {
            return await GetResourceFromXml<IAssemblyResourceInfo>(contentPackage, cfgElement, "Assembly", "FileGroup", _assemblyParserService);
        }
        
        async Task<ImmutableArray<T>> GetResourceFromXml<T>(ContentPackage contentPackage, XElement cfgElement, string elemName, string fileGroupName, IParserServiceAsync<ResourceParserInfo, T> resourceService)
        {
            var elems = GetResourceElementsWithName(owner, cfgElement, elemName, fileGroupName);
            if (elems.IsDefaultOrEmpty)
                return ImmutableArray<T>.Empty;
            
            var results = await resourceService.TryParseResourcesAsync(elems);
            Guard.IsNotEmpty((IReadOnlyCollection<Result<T>>)results, nameof(results));
            
            var resources = ImmutableArray.CreateBuilder<T>();
            foreach (var result in results)
            {
                if (result.Errors.Count > 0)
                {
                    _logger.LogResults(result.ToResult());
                    continue;
                }
                resources.Add(result.Value);
            }
            return resources.ToImmutable();
        }

        ImmutableArray<ResourceParserInfo> GetResourceElementsWithName(ContentPackage package, XElement root, string elemName, string groupName)
        {
            var elems = ImmutableArray.CreateBuilder<ResourceParserInfo>();
            
            elems.AddRange(root.GetChildElements(elemName)
                .Select(e => new ResourceParserInfo(package, e, ImmutableArray<Identifier>.Empty,  ImmutableArray<Identifier>.Empty))
                .ToImmutableArray());
            
            if (root.GetChildElements(groupName).ToImmutableArray() is { IsDefaultOrEmpty: false } fileGroups)
            {
                foreach (var fileGroup in fileGroups)
                {
                    if (fileGroup.GetChildElements(elemName).ToImmutableArray() is { IsDefaultOrEmpty: false } subLuaElems)
                    {
                        var cond = GetDependencyIdentifiers(fileGroup, true);
                        var negCond = GetDependencyIdentifiers(fileGroup, false);

                        foreach (var element in subLuaElems)
                        {
                            elems.Add(new ResourceParserInfo(package, element, cond, negCond));
                        }
                    }
                }
            }

            return elems.ToImmutable();
        }
        
        ImmutableArray<Identifier> GetDependencyIdentifiers(XElement fg, bool depsLoadedSetting)
        {
            return fg.GetChildElements("Conditional")
                .Where(cElem => bool.TryParse(cElem.GetAttribute("IsLoaded").Value, out bool isLoaded) && isLoaded == depsLoadedSetting)
                .SelectMany(cElem2 => cElem2.GetAttributeString("Dependencies", String.Empty)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(ident => new Identifier(ident)))
                .ToImmutableArray();
        }
    }
    
    
    
    private async Task<Result<IModConfigInfo>> CreateFromLegacyAsync(ContentPackage src)
    {
        return new ModConfigInfo()
        {
            Package = src,
            Assemblies = GetAssembliesLegacy(src),
            Configs = GetConfigsLegacy(src),
            LuaScripts = GetLuaScriptsLegacy(src)
        };

        ImmutableArray<IAssemblyResourceInfo> GetAssembliesLegacy(ContentPackage src)
        {
            var binSearchInd = new (string SubFolder, Target Targets, Platform Platforms)[]
            {
                ("bin/Client/Windows", Target.Client, Platform.Windows),
                ("bin/Client/Linux", Target.Client, Platform.Linux),
                ("bin/Client/OSX", Target.Client, Platform.OSX),
                ("bin/Server/Windows", Target.Server, Platform.Windows),
                ("bin/Server/Linux", Target.Server, Platform.Linux),
                ("bin/Server/OSX", Target.Server, Platform.OSX)
            };
            
            var builder = ImmutableArray.CreateBuilder<IAssemblyResourceInfo>();

            foreach (var searchPathways in binSearchInd)
            {
                if (_storageService.FindFilesInPackage(src, searchPathways.SubFolder, "*.dll",
                        true) is { IsSuccess: true, Value.IsDefaultOrEmpty: false } result)
                {
                    builder.Add(new AssemblyResourceInfo()
                    {
                        OwnerPackage = src,
                        InternalName = searchPathways.SubFolder,
                        SupportedPlatforms = searchPathways.Platforms,
                        SupportedTargets = searchPathways.Targets,
                        LoadPriority = 0,
                        FilePaths = result.Value.Select(fp => ContentPath.FromRaw(src, $"%ModDir%/{Path.GetRelativePath(src.Dir, fp)}".CleanUpPathCrossPlatform()))
                            .ToImmutableArray(),
                        FriendlyName = $"{src.Name}.{searchPathways.SubFolder.Replace('/','.')}",
                        IncompatiblePackages = ImmutableArray<Identifier>.Empty,
                        RequiredPackages = ImmutableArray<Identifier>.Empty,
                        IsScript = false,
                        IsReferenceModeOnly = false
                    });
                }
            }

            var sharedResult = _storageService.FindFilesInPackage(src,
                Path.Combine(src.Dir, "CSharp/Shared"),
                "*.cs", true);
            var sharedFiles = sharedResult.IsSuccess && !sharedResult.Value.IsDefaultOrEmpty 
                ? sharedResult.Value.Select(fp => 
                    ContentPath.FromRaw(src, $"%ModDir%/{Path.GetRelativePath(src.Dir, fp)}".CleanUpPathCrossPlatform()))
                    .ToImmutableArray() 
                : ImmutableArray<ContentPath>.Empty;
            
            var srcSearchInd = new (string SubFolder, Target Targets, Platform Platforms)[]
            {
                ("CSharp/Client", Target.Client, Platform.Any),
                ("CSharp/Server", Target.Server, Platform.Any)
            };

            foreach (var searchPathways in srcSearchInd)
            {
                if (_storageService.FindFilesInPackage(src, searchPathways.SubFolder, "*.cs",
                        true) is { IsSuccess: true, Value.IsDefaultOrEmpty: false } result)
                {
                    builder.Add(new AssemblyResourceInfo()
                    {
                        OwnerPackage = src,
                        InternalName = searchPathways.SubFolder,
                        SupportedPlatforms = searchPathways.Platforms,
                        SupportedTargets = searchPathways.Targets,
                        LoadPriority = 0,
                        FilePaths = result.Value
                            .Select(fp => ContentPath.FromRaw(src, 
                                $"%ModDir%/{Path.GetRelativePath(src.Dir, fp)}".CleanUpPathCrossPlatform()))
                            .Concat(sharedFiles).ToImmutableArray(),
                        FriendlyName = IAssemblyLoaderService.InternalsAwareAssemblyName,
                        IncompatiblePackages = ImmutableArray<Identifier>.Empty,
                        RequiredPackages = ImmutableArray<Identifier>.Empty,
                        UseInternalAccessName = true,
                        IsScript = true,
                        IsReferenceModeOnly = false
                    });
                }
            }

            return builder.ToImmutable();
        }
        
        ImmutableArray<IConfigResourceInfo> GetConfigsLegacy(ContentPackage src)
        {
            return ImmutableArray<IConfigResourceInfo>.Empty;
        }
        
        ImmutableArray<ILuaScriptResourceInfo> GetLuaScriptsLegacy(ContentPackage src)
        {
            var builder = ImmutableArray.CreateBuilder<ILuaScriptResourceInfo>();

            if (_storageService.FindFilesInPackage(src, "Lua", "*.lua", true)
                is { IsSuccess: true, Value.IsDefaultOrEmpty: false } result)
            {
                ImmutableArray<string> cleanedResult = result.Value.Select(fp => fp.CleanUpPathCrossPlatform()).ToImmutableArray();

                ImmutableArray<string> autorun = cleanedResult
                    .Where(fp => fp.Contains("Lua/ForcedAutorun/") || fp.Contains("Lua/Autorun/"))
                    .ToImmutableArray();

                ImmutableArray<ContentPath> autorunFP = autorun.Select(fp => ContentPath.FromRaw(src,
                        $"%ModDir%/{Path.GetRelativePath(src.Dir, fp)}".CleanUpPathCrossPlatform()))
                    .ToImmutableArray();

                ImmutableArray<ContentPath> reg = cleanedResult.Except(autorun)
                    .Select(fp => ContentPath.FromRaw(src, 
                        $"%ModDir%/{Path.GetRelativePath(src.Dir, fp)}".CleanUpPathCrossPlatform()))
                    .ToImmutableArray();
                
                builder.Add(new LuaScriptsResourceInfo()
                {
                    OwnerPackage = src,
                    InternalName = "LegacyAutorun",
                    SupportedPlatforms = Platform.Any,
                    SupportedTargets = Target.Any,
                    LoadPriority = 1,   // autorun should be last to ensure that dependent code in other files are loaded first
                    FilePaths = autorunFP,
                    IncompatiblePackages =  ImmutableArray<Identifier>.Empty,
                    RequiredPackages = ImmutableArray<Identifier>.Empty,
                    IsAutorun = true,
                });
                
                builder.Add(new LuaScriptsResourceInfo()
                {
                    OwnerPackage = src,
                    InternalName = "Legacy",
                    SupportedPlatforms = Platform.Any,
                    SupportedTargets = Target.Any,
                    LoadPriority = 0,   // should be included first to ensure that dependent code in these files are available
                    FilePaths = reg,
                    IncompatiblePackages =  ImmutableArray<Identifier>.Empty,
                    RequiredPackages = ImmutableArray<Identifier>.Empty,
                    IsAutorun = false,
                });
            }
            
            return builder.ToImmutable();
        }
        
    }
}
