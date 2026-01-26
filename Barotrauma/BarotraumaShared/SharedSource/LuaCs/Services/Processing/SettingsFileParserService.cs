using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.Toolkit.Diagnostics;
using OneOf;

namespace Barotrauma.LuaCs.Services.Processing;

public sealed class SettingsFileParserService : 
    IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigInfo>,
    IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigProfileInfo>
{
    #region DisposalControl

    private AsyncReaderWriterLock _operationLock = new();

    public void Dispose()
    {
        using var lck = _operationLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
        {
            return;
        }
        _storageService.Dispose();
        _storageService = null;
    }

    private int _isDisposed = 0;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    #endregion

    private IStorageService _storageService;
    
    public SettingsFileParserService(IStorageService storageService)
    {
        _storageService = storageService;
    }
    
    async Task<Result<ImmutableArray<IConfigInfo>>> IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigInfo>
        .TryParseResourcesAsync(IConfigResourceInfo src)
    {
        Guard.IsNotNull(src, nameof(src));
        Guard.IsNotNull(src.OwnerPackage, nameof(src.OwnerPackage));
        using var lck = await _operationLock.AcquireReaderLock();
        IService.CheckDisposed(this);

        if (src.FilePaths.IsDefaultOrEmpty)
        {
            return ReturnFail($"The config file list is empty.");
        }
        
        var parsedInfo = ImmutableArray.CreateBuilder<IConfigInfo>();
        
        foreach ((ContentPath path, Result<XDocument> docLoadResult) res in await _storageService.LoadPackageXmlFilesAsync(src.FilePaths))
        {
            if (res.docLoadResult.IsFailed)
            {
                return ReturnFail($"Failed to load document for {src.OwnerPackage.Name}").WithErrors(res.docLoadResult.Errors);
            }
            
            var settingElements = res.docLoadResult.Value.GetChildElement("Configuration")
                .GetChildElements("Settings").SelectMany(e => e.GetChildElements("Setting")).ToImmutableArray();
            if (settingElements.IsDefaultOrEmpty)
            {
                continue;
            }

            var packageIdent = res.path.ContentPackage.ToIdentifier().ToString();
            
            foreach (var element in settingElements)
            {
                var name = element.GetAttributeString("Name", string.Empty);
                if (name.IsNullOrWhiteSpace())
                {
                    return ReturnFail(
                        $"The internal name for a setting in the config file '{res.path.FullPath}' is empty!");
                }
                
                var newSetting = new ConfigInfo()
                {
                    InternalName = name,
                    OwnerPackage = res.path.ContentPackage,
                    DataType = element.GetAttributeString("Type", string.Empty),
                    Element = element,
                    EditableStates = element.GetAttributeBool("AllowChangesWhileExecuting", true) ? RunState.Running :
                        element.GetAttributeBool("ReadOnly", false) ? RunState.LoadedNoExec :
                        RunState.Unloaded,
                    NetSync = element.GetAttributeEnum("NetSync", NetSync.None),
#if CLIENT
                    DisplayName = $"{packageIdent}.{name}.DisplayName",
                    Description = $"{packageIdent}.{name}.Description",
                    DisplayCategory = $"{packageIdent}.{name}.DisplayCategory",
                    ShowInMenus = element.GetAttributeBool("ShowInMenus", true),
                    Tooltip = $"{packageIdent}.{name}.Tooltip",
                    ImageIconPath = element.GetAttributeString("ImageIcon", string.Empty) is {} val && !val.IsNullOrWhiteSpace() ?
                        ContentPath.FromRaw(res.path.ContentPackage, val) : ContentPath.Empty
#endif
                };
                if (!IsInfoValid(newSetting))
                {
                    return ReturnFail($"A setting was invalid. ContentPackage: {res.path.ContentPackage}");
                }
                parsedInfo.Add(newSetting);
            }
        }
        
        return FluentResults.Result.Ok(parsedInfo.ToImmutable());
        
        // Helpers

        FluentResults.Result ReturnFail(string msg)
        {
            return FluentResults.Result.Fail($"{nameof(IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigInfo>.TryParseResourcesAsync)}: {msg}");
        }

        bool IsInfoValid(ConfigInfo info)
        {
            return info.OwnerPackage != null
                   && !info.InternalName.IsNullOrWhiteSpace()
                   && !info.DataType.IsNullOrWhiteSpace()
                   && !info.DataType.IsNullOrWhiteSpace()
                   && info.Element != null
#if CLIENT
                   && !info.DisplayName.IsNullOrWhiteSpace()
                   && !info.Description.IsNullOrWhiteSpace()
                   && !info.DisplayCategory.IsNullOrWhiteSpace()
                   && !info.Tooltip.IsNullOrWhiteSpace()
#endif
                ;
        }
    }
    
    async Task<Result<ImmutableArray<IConfigProfileInfo>>>
        IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigProfileInfo>
        .TryParseResourcesAsync(IConfigResourceInfo src)
    {
        Guard.IsNotNull(src, nameof(src));
        Guard.IsNotNull(src.OwnerPackage, nameof(src.OwnerPackage));
        using var lck = await _operationLock.AcquireReaderLock();
        IService.CheckDisposed(this);

        if (src.FilePaths.IsDefaultOrEmpty)
        {
            return ReturnFail($"The config file list is empty.");
        }

        var parsedInfo = ImmutableArray.CreateBuilder<IConfigProfileInfo>();

        foreach ((ContentPath path, Result<XDocument> docLoadResult) res in await _storageService
                     .LoadPackageXmlFilesAsync(src.FilePaths))
        {
            if (res.docLoadResult.IsFailed)
            {
                return ReturnFail($"Failed to load document for {src.OwnerPackage.Name}")
                    .WithErrors(res.docLoadResult.Errors);
            }

            var profileCollection = res.docLoadResult.Value.GetChildElement("Configuration")
                .GetChildElement("Profiles");
            if (profileCollection == null)
            {
                continue;
            }

            foreach (var profile in profileCollection.GetChildElements("Profile"))
            {
                var profileName = profile.GetAttributeString("Name", string.Empty);
                Guard.IsNotNullOrWhiteSpace(profileName, nameof(profileName));
                
                var settingValues = profile.GetChildElements("SettingValue").ToImmutableArray();
                if (settingValues.IsDefaultOrEmpty)
                {
                    ThrowHelper.ThrowArgumentNullException(nameof(settingValues));
                }

                var profileValuesBuilder = ImmutableArray.CreateBuilder<(string ConfigName, XElement Value)>();

                foreach (var settingValue in settingValues)
                {
                    var cfgName = settingValue.GetAttributeString("Name", string.Empty);
                    Guard.IsNotNullOrWhiteSpace(cfgName, nameof(cfgName));
                    profileValuesBuilder.Add((cfgName, settingValue));
                }
                
                parsedInfo.Add(new ConfigProfileInfo()
                {
                    InternalName = profileName,
                    OwnerPackage = res.path.ContentPackage,
                    ProfileValues = profileValuesBuilder.ToImmutable()
                });
            }
        }
        
        return parsedInfo.ToImmutable();

        FluentResults.Result ReturnFail(string msg)
        {
            return FluentResults.Result.Fail($"{nameof(IParserServiceOneToManyAsync<IConfigResourceInfo, IConfigProfileInfo>.TryParseResourcesAsync)}: {msg}");
        }
    }

}
