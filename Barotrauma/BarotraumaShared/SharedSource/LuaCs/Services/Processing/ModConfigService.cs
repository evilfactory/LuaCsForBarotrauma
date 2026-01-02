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

namespace Barotrauma.LuaCs.Services.Processing;

public sealed class ModConfigService : IModConfigService
{
    private IStorageService  _storageService;
    private IParserServiceAsync<XElement, IAssemblyResourceInfo> _assemblyParserService;
    private IParserServiceAsync<XElement, ILuaScriptResourceInfo>  _luaScriptParserService;
    private IParserServiceAsync<XElement, IConfigResourceInfo>  _configParserService;
    private IParserServiceAsync<XElement, IConfigProfileResourceInfo>  _configProfileParserService;
    
    public ModConfigService(IStorageService storageService, 
        IParserServiceAsync<XElement, IAssemblyResourceInfo> assemblyParserService, 
        IParserServiceAsync<XElement, ILuaScriptResourceInfo> luaScriptParserService, 
        IParserServiceAsync<XElement, IConfigResourceInfo> configParserService, 
        IParserServiceAsync<XElement, IConfigProfileResourceInfo> configProfileParserService)
    {
        _storageService = storageService;
        _assemblyParserService = assemblyParserService;
        _luaScriptParserService = luaScriptParserService;
        _configParserService = configParserService;
        _configProfileParserService = configProfileParserService;
    } 
    
    #region Disposal

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private int _isDisposed = 0;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        protected set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    #endregion

    public async Task<Result<IModConfigInfo>> CreateConfigAsync(ContentPackage src)
    {
        if (src is null)
            ArgumentNullException.ThrowIfNull($"{nameof(CreateConfigAsync)}: Source is null.");
        
        if (await TryGetModConfigXmlAsync(src) is { IsSuccess: true, Value: { } config })
        {
            return await CreateFromConfigXmlAsync(config);
        }
        
        return await CreateFromLegacyAsync(src);
    }

    public async Task<ImmutableArray<(ContentPackage Source, Result<IModConfigInfo> Config)>> CreateConfigsAsync(ImmutableArray<ContentPackage> src)
    {
        var builder = ImmutableArray.CreateBuilder<(ContentPackage Source, Result<IModConfigInfo> Config)>();

        foreach (var package in src)
        {
            builder.Add((package, await CreateConfigAsync(package)));
        }
        
        return builder.ToImmutable();
    }

    //--- Helpers
    private async Task<Result<XElement>> TryGetModConfigXmlAsync(ContentPackage src)
    {
        
    }

    private async Task<Result<IModConfigInfo>> CreateFromConfigXmlAsync(XElement src)
    {
        throw new NotImplementedException();
    }
    
    private async Task<Result<IModConfigInfo>> CreateFromLegacyAsync(ContentPackage src)
    {
        throw new NotImplementedException();
    }
}
