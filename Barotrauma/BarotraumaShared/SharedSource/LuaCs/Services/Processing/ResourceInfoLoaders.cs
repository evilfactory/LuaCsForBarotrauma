using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services.Processing;

public class ResourceInfoLoaders : IConverterServiceAsync<ILocalizationResourceInfo, ImmutableArray<ILocalizationInfo>>
{
    private readonly IStorageService _storageService;
    private readonly IConfigServiceConfig _configServiceConfig;

    public ResourceInfoLoaders(IStorageService storageService, IConfigServiceConfig configServiceConfig)
    {
        _storageService = storageService;
        _configServiceConfig = configServiceConfig;
    }
    
    
    /// <summary>
    /// This class is stateless and cannot clear its dependency references.
    /// </summary>
    public void Dispose() {}
    
    /// <summary>
    /// This class is stateless and cannot clear its dependency references.
    /// </summary>
    public bool IsDisposed => false;

    
    public async Task<Result<ImmutableArray<ILocalizationInfo>>> TryParseResourceAsync(ILocalizationResourceInfo src)
    {
        try
        {
            if (src is null || src.FilePaths.IsDefaultOrEmpty || src.OwnerPackage is null)
                return FluentResults.Result.Fail($"{nameof(TryParseResourceAsync)}: Source(s) was null or empty.");

            var filesXml = await _storageService.LoadPackageXmlFilesAsync(src.OwnerPackage, src.FilePaths);

            var res = new FluentResults.Result<ILocalizationInfo>();

            var arrDict = new Dictionary<string, ImmutableArray<(CultureInfo Culture, string Value)>.Builder>();
            
            foreach (var fileResult in filesXml)
            {
                if (fileResult.Item2.Errors.Any())
                    res = res.WithErrors(fileResult.Item2.Errors);
                if (fileResult.Item2.IsFailed)
                    continue;
                if (fileResult.Item2.Value.Root is not { } root||  root.Name.LocalName.ToLowerInvariant() != "localization")
                    continue;
                
                // parse root
                if (root.GetChildElements("Culture") is {} cultureDefs)
                {
                    foreach (var cultureDef in cultureDefs)
                    {
                        try
                        {
                            var tgtCulture = CultureInfo.GetCultureInfo(cultureDef.GetAttributeString("Key", "en-us"));
                            if (cultureDef.GetChildElements("Text").ToImmutableArray() is { IsDefaultOrEmpty: false } textDefs)
                            {
                                foreach (var textDef in textDefs)
                                {
                                    if (textDef.GetAttributeString("Key", null) is { } keyDef
                                        && !keyDef.IsNullOrWhiteSpace()
                                        && !textDef.IsEmpty 
                                        && !textDef.Value.IsNullOrWhiteSpace())
                                    {
                                        if (!arrDict.ContainsKey(keyDef))
                                            arrDict[keyDef] = ImmutableArray.CreateBuilder<(CultureInfo Culture, string Value)>();
                                        arrDict[keyDef].Add((Culture: tgtCulture, Value: textDef.Value));
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            res = res.WithError($"Error while parsing a culture in localization file: {fileResult.Item1}")
                                .WithError(new ExceptionalError(e));
                            continue;
                        }
                    }
                }
            }
            
            if (arrDict.Count < 1)
                return FluentResults.Result.Fail($"{nameof(TryParseResourceAsync)}: No resources found.");

            var builder = ImmutableArray.CreateBuilder<ILocalizationInfo>();

            foreach (var kvp in arrDict)
            {
                var translations = kvp.Value.MoveToImmutable();
                if (translations.IsDefaultOrEmpty)
                    continue;
                
                var loc = new LocalizationInfo()
                {
                    OwnerPackage = src.OwnerPackage,
                    InternalName = src.InternalName,
                    LoadPriority = src.LoadPriority,
                    Key = kvp.Key,
                    Translations = translations
                };
                builder.Add(loc);
            }

            return FluentResults.Result.Ok(builder.MoveToImmutable())
                .WithErrors(res.Errors);
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail($"Unable to load file. Error: {e.Message}.");
        } 
        
        
    }

    public async Task<ImmutableArray<Result<ImmutableArray<ILocalizationInfo>>>> TryParseResourcesAsync(IEnumerable<ILocalizationResourceInfo> sources)
    {
        var results = new ConcurrentQueue<Result<ImmutableArray<ILocalizationInfo>>>();

        var src = sources.ToImmutableArray();
        if (!src.Any())
            return ImmutableArray<Result<ImmutableArray<ILocalizationInfo>>>.Empty;

        await src.ParallelForEachAsync(async loc =>
        {
            var res = await TryParseResourceAsync(loc);
            results.Enqueue(res);
        }, 2);  // we only need 2 parallels to buffer against disk loading.
        
        return results.ToImmutableArray();
    }

    

    
}
