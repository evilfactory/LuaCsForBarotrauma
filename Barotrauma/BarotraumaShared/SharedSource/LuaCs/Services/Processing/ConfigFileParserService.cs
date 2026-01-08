using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.Toolkit.Diagnostics;

namespace Barotrauma.LuaCs.Services.Processing;

public sealed class ConfigFileParserService : 
    IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo>, 
    IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo>, 
    IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>
{
    private IStorageService _storageService;
    private readonly AsyncReaderWriterLock _operationsLock = new();

    public ConfigFileParserService(IStorageService storageService)
    {
        _storageService = storageService;
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
            this._storageService = null;
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

    // --- Assemblies
    async Task<Result<IAssemblyResourceInfo>> IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo>.TryParseResourceAsync(ResourceParserInfo src)
    {
        using var lck = await _operationsLock.AcquireWriterLock();
        IService.CheckDisposed(this);
        
        if (CheckThrowNullRefs(src, "Assembly") is { IsFailed: true } fail)
            return fail;

        var runtimeEnv = GetRuntimeEnvironment(src.Element);
        var fileResults = await GetCheckedFiles(src.Element, src.Owner, ".dll");
        
        if (fileResults.IsFailed)
            return FluentResults.Result.Fail(fileResults.Errors);

        return new AssemblyResourceInfo()
        {
            SupportedPlatforms = runtimeEnv.Platform,
            SupportedTargets =  runtimeEnv.Target,
            LoadPriority = src.Element.GetAttributeInt("LoadPriority", 0),
            FilePaths = fileResults.Value,
            Optional =  src.Element.GetAttributeBool("Optional", false),
            InternalName = src.Element.GetAttributeString("Name", string.Empty),
            OwnerPackage =  src.Owner,
            RequiredPackages = src.Required,
            IncompatiblePackages =  src.Incompatible,
            // Type Specific
            FriendlyName = src.Element.GetAttributeString("FriendlyName", string.Empty),
            IsScript = src.Element.GetAttributeBool("IsScript", false),
        };
    }
    
    async Task<ImmutableArray<Result<IAssemblyResourceInfo>>> IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo>.TryParseResourcesAsync(IEnumerable<ResourceParserInfo> sources)
    {
        return await this.TryParseGenericResourcesAsync<IAssemblyResourceInfo>(sources);
    }

    // --- Config
    
    async Task<Result<IConfigResourceInfo>> IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>.TryParseResourceAsync(ResourceParserInfo src)
    {
        
        using var lck = await _operationsLock.AcquireWriterLock();
        IService.CheckDisposed(this);
        
        if (CheckThrowNullRefs(src, "Config") is { IsFailed: true } fail)
            return fail;

        var runtimeEnv = GetRuntimeEnvironment(src.Element);
        var fileResults = await GetCheckedFiles(src.Element, src.Owner, ".xml");
        
        if (fileResults.IsFailed)
            return FluentResults.Result.Fail(fileResults.Errors);

        return new ConfigResourceInfo()
        {
            SupportedPlatforms = runtimeEnv.Platform,
            SupportedTargets =  runtimeEnv.Target,
            LoadPriority = src.Element.GetAttributeInt("LoadPriority", 0),
            FilePaths = fileResults.Value,
            Optional =  src.Element.GetAttributeBool("Optional", false),
            InternalName = src.Element.GetAttributeString("Name", string.Empty),
            OwnerPackage =  src.Owner,
            RequiredPackages = src.Required,
            IncompatiblePackages =  src.Incompatible
        };
    }
    
    async Task<ImmutableArray<Result<IConfigResourceInfo>>> IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>.TryParseResourcesAsync(IEnumerable<ResourceParserInfo> sources)
    {
        return await this.TryParseGenericResourcesAsync<IConfigResourceInfo>(sources);
    }

    // --- Lua Scripts    
    async Task<Result<ILuaScriptResourceInfo>> IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo>.TryParseResourceAsync(ResourceParserInfo src)
    {
        using var lck = await _operationsLock.AcquireWriterLock();
        IService.CheckDisposed(this);
        
        if (CheckThrowNullRefs(src, "Lua") is { IsFailed: true } fail)
            return fail;

        var runtimeEnv = GetRuntimeEnvironment(src.Element);
        var fileResults = await GetCheckedFiles(src.Element, src.Owner, ".lua");
        
        if (fileResults.IsFailed)
            return FluentResults.Result.Fail(fileResults.Errors);

        return new LuaScriptsResourceInfo()
        {
            SupportedPlatforms = runtimeEnv.Platform,
            SupportedTargets =  runtimeEnv.Target,
            LoadPriority = src.Element.GetAttributeInt("LoadPriority", 0),
            FilePaths = fileResults.Value,
            Optional =  src.Element.GetAttributeBool("Optional", false),
            InternalName = src.Element.GetAttributeString("Name", string.Empty),
            OwnerPackage =  src.Owner,
            RequiredPackages = src.Required,
            IncompatiblePackages =  src.Incompatible,
            // Type Specific
            IsAutorun = src.Element.GetAttributeBool("RunFile", false)
        };
    }

    private FluentResults.Result CheckThrowNullRefs(ResourceParserInfo src, string elementName)
    {
        Guard.IsNotNull(src, nameof(src));
        Guard.IsNotNull(src.Owner, nameof(src.Owner));
        Guard.IsNotNull(src.Element, nameof(src.Element));
        
        if (src.Element.Name != elementName)
        {
            return FluentResults.Result.Fail($"Element name '{elementName}' is incorrect");
        }
        
        return FluentResults.Result.Ok();
    }

    async Task<ImmutableArray<Result<ILuaScriptResourceInfo>>> IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo>.TryParseResourcesAsync(IEnumerable<ResourceParserInfo> sources)
    {
        return await this.TryParseGenericResourcesAsync<ILuaScriptResourceInfo>(sources);
    }
    
    // --- Helpers
    private async Task<Result<ImmutableArray<ContentPath>>> GetCheckedFiles(XElement srcElement, ContentPackage srcOwner, string fileExtension)
    {
        using var lck = await _operationsLock.AcquireWriterLock();
        IService.CheckDisposed(this);
        
        var builder = ImmutableArray.CreateBuilder<ContentPath>();
        var filePath = srcElement.GetAttributeString("File",  string.Empty);
        var folderPath = srcElement.GetAttributeString("Folder",  string.Empty);

        if (!filePath.IsNullOrWhiteSpace())
        {
            var cp = ContentPath.FromRaw(srcOwner, filePath);
            if (_storageService.FileExists(cp.FullPath) is { IsSuccess: true, Value: true })
            {
                builder.Add(cp);
            }
        }

        if (!folderPath.IsNullOrWhiteSpace())
        {
            var cp = ContentPath.FromRaw(srcOwner, folderPath);
            if (_storageService.DirectoryExists(cp.FullPath) is { IsSuccess: true, Value: true })
            {
                var files = _storageService.FindFilesInPackage(cp.ContentPackage, cp.Value, fileExtension, true);
            }
        }
            
        throw new NotImplementedException();
    }    
    private (Platform Platform, Target Target) GetRuntimeEnvironment(XElement element)
    {
        return (
            Platform: element.GetAttributeEnum("Platform", Platform.Windows |  Platform.Linux | Platform.OSX),
            Target: element.GetAttributeEnum("Target", Target.Client | Target.Server));
    }
    
    private async Task<ImmutableArray<Result<T>>> TryParseGenericResourcesAsync<T>(IEnumerable<ResourceParserInfo> sources)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        Guard.IsNotNull(sources,  nameof(IParserServiceAsync<ResourceParserInfo, T>.TryParseResourcesAsync));
        var builder =  ImmutableArray.CreateBuilder<Result<T>>();
        foreach (var info in sources)
        {
            builder.Add(await Unsafe.As<IParserServiceAsync<ResourceParserInfo, T>>(this).TryParseResourceAsync(info));
        }
        return builder.ToImmutable();
    }
    
}
