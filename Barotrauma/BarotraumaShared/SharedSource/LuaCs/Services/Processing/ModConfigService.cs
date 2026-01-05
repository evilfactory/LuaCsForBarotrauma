using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.Toolkit.Diagnostics;
using MoonSharp.VsCodeDebugger.SDK;

namespace Barotrauma.LuaCs.Services.Processing;

public sealed class ModConfigService : IModConfigService
{
    private IStorageService  _storageService;
    private IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo> _assemblyParserService;
    private IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo>  _luaScriptParserService;
    private IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>  _configParserService;
    private IParserServiceAsync<ResourceParserInfo, IConfigProfileResourceInfo>  _configProfileParserService;
    
    public ModConfigService(IStorageService storageService, 
        IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo> assemblyParserService, 
        IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo> luaScriptParserService, 
        IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo> configParserService, 
        IParserServiceAsync<ResourceParserInfo, IConfigProfileResourceInfo> configProfileParserService)
    {
        _storageService = storageService;
        _assemblyParserService = assemblyParserService;
        _luaScriptParserService = luaScriptParserService;
        _configParserService = configParserService;
        _configProfileParserService = configProfileParserService;
    } 
    
    #region Dispose

    public void Dispose()
    {
        throw new NotImplementedException();
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
        
        if (await TryGetModConfigXmlAsync(src) is { IsSuccess: true, Value: { } config })
        {
            return await CreateFromConfigXmlAsync(src, config);
        }
        
        return await CreateFromLegacyAsync(src);
    }

    public async Task<ImmutableArray<(ContentPackage Source, Result<IModConfigInfo> Config)>> CreateConfigsAsync(ImmutableArray<ContentPackage> src)
    {
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
        return await _storageService.LoadPackageXmlAsync(src, "ModConfig.xml") is { IsSuccess: true, Value: { Root: {} config} } 
            ? FluentResults.Result.Ok(config) 
            : FluentResults.Result.Fail<XElement>("ModConfig.xml not found");
    }

    private async Task<Result<IModConfigInfo>> CreateFromConfigXmlAsync(ContentPackage owner, XElement src)
    {
        /*var cfg = src.GetChildElements("Config");
        var modConfig = new ModConfigInfo()
        {
            Package = owner,
            Assemblies = src.GetChildElements("Assembly") is {} asm ? GetAssembliesFromXml(owner, asm) 
                : ImmutableArray<IAssemblyResourceInfo>.Empty,
            Configs = cfg is {} ? GetConfigsFromXml(owner, cfg) : ImmutableArray<IConfigResourceInfo>.Empty,
            ConfigProfiles = cfg is {} ? GetConfigProfilesFromXml(owner, cfg) :  ImmutableArray<IConfigProfileResourceInfo>.Empty,
            LuaScripts = src.GetChildElements("Lua") is {} lua ? GetLuaScriptsFromXml(owner, lua) 
                : ImmutableArray<ILuaScriptResourceInfo>.Empty
        };*/

        async Task<FluentResults.Result<ImmutableArray<ILuaScriptResourceInfo>>> GetLuaScriptsFromXml(ContentPackage contentPackage, 
            XElement cfgElement)
        {
            var luaElems = cfgElement.GetChildElements("Lua").ToImmutableArray();
            if (cfgElement.GetChildElements("FileGroup").ToImmutableArray() is { IsDefaultOrEmpty: false } fileGroup
                && fileGroup.SelectMany(fg => fg.GetChildElements()))
            {
                
            }
            
            
            throw new NotImplementedException();
        }

        async Task<FluentResults.Result<ImmutableArray<IConfigProfileResourceInfo>>> GetConfigProfilesFromXml(ContentPackage contentPackage, 
            XElement cfgElement)
        {
            throw new NotImplementedException();
        }

        async Task<FluentResults.Result<ImmutableArray<IConfigResourceInfo>>> GetConfigsFromXml(ContentPackage contentPackage, 
            XElement cfgElement)
        {
            throw new NotImplementedException();
        }

        async Task<FluentResults.Result<ImmutableArray<IAssemblyResourceInfo>>> GetAssembliesFromXml(ContentPackage contentPackage, 
            XElement cfgElement)
        {
            throw new NotImplementedException();
        }
    }
    
    
    
    private async Task<Result<IModConfigInfo>> CreateFromLegacyAsync(ContentPackage src)
    {
        throw new NotImplementedException();
    }
}
