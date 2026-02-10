using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.Toolkit.Diagnostics;

namespace Barotrauma.LuaCs;

public sealed class ModConfigFileParserService : 
    IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo>, 
    IParserServiceAsync<ResourceParserInfo, ILuaScriptResourceInfo>, 
    IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>
{
    private IStorageService _storageService;
    private readonly AsyncReaderWriterLock _operationsLock = new();

    public ModConfigFileParserService(IStorageService storageService)
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
        using var lck = await _operationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        
        if (CheckThrowNullRefs(src, "Assembly") is { IsFailed: true } fail)
            return fail;

        var runtimeEnv = GetRuntimeEnvironment(src.Element);
        var fileResults = await UnsafeGetCheckedFiles(src.Element, src.Owner, ".dll");
        
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
            UseInternalAccessName = src.Element.GetAttributeBool("UseInternalAccessName", false),
            IsReferenceModeOnly = src.Element.GetAttributeBool("IsReferenceModeOnly", false)
        };
    }
    
    async Task<ImmutableArray<Result<IAssemblyResourceInfo>>> IParserServiceAsync<ResourceParserInfo, IAssemblyResourceInfo>.TryParseResourcesAsync(IEnumerable<ResourceParserInfo> sources)
    {
        return await this.TryParseGenericResourcesAsync<IAssemblyResourceInfo>(sources);
    }

    // --- Config
    
    async Task<Result<IConfigResourceInfo>> IParserServiceAsync<ResourceParserInfo, IConfigResourceInfo>.TryParseResourceAsync(ResourceParserInfo src)
    {
        using var lck = await _operationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        
        if (CheckThrowNullRefs(src, "Config") is { IsFailed: true } fail)
            return fail;

        var runtimeEnv = GetRuntimeEnvironment(src.Element);
        var fileResults = await UnsafeGetCheckedFiles(src.Element, src.Owner, ".xml");
        
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
        using var lck = await _operationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        
        if (CheckThrowNullRefs(src, "Lua") is { IsFailed: true } fail)
            return fail;

        var runtimeEnv = GetRuntimeEnvironment(src.Element);
        var fileResults = await UnsafeGetCheckedFiles(src.Element, src.Owner, ".lua");
        
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
            IsAutorun = src.Element.GetAttributeBool("IsAutorun", false)
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
    private async Task<Result<ImmutableArray<ContentPath>>> UnsafeGetCheckedFiles(XElement srcElement, ContentPackage srcOwner, string fileExtension)
    {
        var builder = ImmutableArray.CreateBuilder<ContentPath>();
        var filePath = srcElement.GetAttributeContentPath("File",  srcOwner);
        var folderPath = srcElement.GetAttributeContentPath("Folder",  srcOwner);

        var res = new FluentResults.Result<ImmutableArray<ContentPath>>();
        
        if (!filePath.IsNullOrWhiteSpace())
        {
            if (_storageService.FileExists(filePath.FullPath) is { IsSuccess: true, Value: true })
            {
                builder.Add(filePath);
            }
            else
            {
                res.WithError($"{srcOwner.Name}: The file '{filePath}' is missing!");
            }
        }

        if (!folderPath.IsNullOrWhiteSpace())
        {
            if (_storageService.DirectoryExists(folderPath.FullPath) is { IsSuccess: true, Value: true })
            {
                var files = _storageService.FindFilesInPackage(srcOwner, folderPath.Value, fileExtension, true);
            }
            else
            {
                res.WithError($"{srcOwner.Name}: The folder '{filePath}' is missing!");
            }
        }

        return res.WithValue(builder.ToImmutable());
    }    
    private (Platform Platform, Target Target) GetRuntimeEnvironment(XElement element)
    {
        return (
            Platform: element.GetAttributeEnum("Platform", Platform.Any),
            Target: element.GetAttributeEnum("Target", Target.Any));
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
