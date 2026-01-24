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

namespace Barotrauma.LuaCs.Services.Processing;

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
        
        var builder = new ConcurrentQueue<(ContentPackage Source, Result<IModConfigInfo> Config)>();

        await src.ParallelForEachAsync(async package =>
        {
            var res = await CreateConfigAsync(package);
            builder.Enqueue((package, res));
        });

        return builder.OrderBy(pkg => src.IndexOf(pkg.Source)).ToImmutableArray();
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
        ImmutableArray<IAssemblyResourceInfo> assemblyResources = default;
        ImmutableArray<IConfigResourceInfo> configResources = default;
        ImmutableArray<ILuaScriptResourceInfo> luaResources = default;

        var tasks = new[]
        {
            new Task<Task>(async () => assemblyResources = await GetAssembliesFromXml(owner, src)),
            new Task<Task>(async () => configResources = await GetConfigsFromXml(owner, src)),
            new Task<Task>(async () => luaResources = await GetLuaScriptsFromXml(owner, src)),
        };

        tasks.ForEach(t => t.Start());
        var res = await Task.WhenAll(tasks);

        bool isFaulted = false;
        foreach (var task in res)
        {
            if (task.IsFaulted)
            {
                _logger.LogError($"{nameof(CreateFromConfigXmlAsync)}: {task.Exception?.ToString()}");
                isFaulted = true;
            }
        }

        if (isFaulted)
        {
            _logger.LogError($"{nameof(CreateFromConfigXmlAsync)}: Failed to process content package: {owner.Name}");
            return FluentResults.Result.Fail($"{nameof(CreateFromConfigXmlAsync)}: Failed to process content package: {owner.Name}");
        }

        return FluentResults.Result.Ok<IModConfigInfo>(new ModConfigInfo()
        {
            Package = owner,
            Assemblies = assemblyResources,
            Configs = configResources,
            LuaScripts = luaResources
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
            return resources.MoveToImmutable();
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

            return elems.MoveToImmutable();
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
        // TODO: Implement legacy mod analysis
        return new ModConfigInfo() 
        { 
            Package = src, 
            Assemblies = ImmutableArray<IAssemblyResourceInfo>.Empty, 
            Configs = ImmutableArray<IConfigResourceInfo>.Empty, 
            LuaScripts = ImmutableArray<ILuaScriptResourceInfo>.Empty 
        };
    }
}
