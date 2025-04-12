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

public class ResourceInfoLoaders : IConverterServiceAsync<ILocalizationResourceInfo, ImmutableArray<ILocalizationInfo>>,
    IConverterServiceAsync<IConfigResourceInfo, IReadOnlyList<IConfigInfo>>,
    IConverterServiceAsync<IConfigProfileResourceInfo, IReadOnlyList<IConfigProfileInfo>>
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

    public async Task<Result<IReadOnlyList<IConfigInfo>>> TryParseResourceAsync(IConfigResourceInfo src)
    {
        if (src?.OwnerPackage is null || src.FilePaths.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(TryParseResourceAsync)}: Config resource and/or components were null.");
        
        try
        {
            var infos = await _storageService.LoadPackageXmlFilesAsync(src.OwnerPackage, src.FilePaths);
            if (infos.IsDefaultOrEmpty)
                return FluentResults.Result.Fail($"{nameof(TryParseResourceAsync)}: No resources found.");

            var errList = new List<IError>();

            var resList = infos.Select(info =>
                {
                    if (info.Item2.Errors.Any())
                        errList.AddRange(info.Item2.Errors);
                    if (info.Item2.IsFailed || info.Item2.Value is not { } configXDoc)
                    {
                        errList.Add(new Error($"Unable to parse file: {info.Item1}"));
                        return default;
                    }

                    return (info.Item1, configXDoc);
                })
                .Where(doc => !doc.Item1.IsNullOrWhiteSpace() && doc.configXDoc != null)
                .SelectMany(doc => doc.configXDoc.Root.GetChildElements("Configuration")
                    .Select(element => (doc.Item1, element)))
                .SelectMany(cfgContainer => cfgContainer.element.GetChildElements("Configs")
                        .Select(element => (cfgContainer.Item1, element)))
                .SelectMany(cfgContainer => cfgContainer.element.GetChildElements("Config")
                        .Select(element => (cfgContainer.Item1, element)))
                .Select(async pathElement =>
                {
                    var cfgElement = pathElement.element;
                    
                    try
                    {
                        OneOf.OneOf<string, XElement> defaultValue = cfgElement.GetChildElement("Value");
                        if (defaultValue.AsT1 is null)
                            defaultValue = cfgElement.GetAttributeString("Value", string.Empty);

                        var internalName = cfgElement.GetAttributeString("Name", pathElement.Item1);

                        var localRes = await _storageService.LoadLocalXmlAsync(src.OwnerPackage,
                            _configServiceConfig.LocalConfigPathPartial.Replace(
                                _configServiceConfig.FileNamePattern, 
                                SanitizedFileName(src.OwnerPackage.Name)));

                        OneOf.OneOf<string, XElement> val;

                        if (localRes.IsFailed || localRes.Value is not { Root: { } root } 
                                              || root.GetChildElement(internalName)?.GetChildElement("Value") is not {} value )
                        {
                            val = defaultValue;
                        }
                        else
                        {
                            val = value;
                        }
                        
                        return new ConfigInfo()
                        {
                            DataType = Type.GetType(cfgElement.GetAttributeString("Type", "string")),
                            OwnerPackage = src.OwnerPackage,
                            DefaultValue = defaultValue,
                            Value = val,
                            EditableStates = cfgElement.GetAttributeBool("ReadOnly", false)
                                ? RunState.Unloaded // read-only
                                : RunState.Running, // editable at runtime
                            InternalName = internalName,
                            NetSync = Enum.Parse<NetSync>(
                                cfgElement.GetAttributeString("NetSync", nameof(NetSync.None))),
#if CLIENT
                            DisplayName = cfgElement.GetAttributeString("DisplayName", null),
                            Description = cfgElement.GetAttributeString("Description", null),
                            DisplayCategory = cfgElement.GetAttributeString("Category", null),
                            ShowInMenus = cfgElement.GetAttributeBool("ShowInMenus", true),
                            Tooltip = cfgElement.GetAttributeString("Tooltip", null),
                            ImageIconPath = cfgElement.GetAttributeString("Image", null)
#endif
                        };
                    }
                    catch (Exception e)
                    {
                        errList.Add(new Error($"Failed to parse config var for package {src.OwnerPackage}"));
                        errList.Add(new ExceptionalError(e));
                        return null;
                    }
                })
                .Where(task => task is not null)
                .ToImmutableArray();

            var result = (await Task.WhenAll(resList)).ToImmutableArray();

            var ret = FluentResults.Result.Ok((IReadOnlyList<IConfigInfo>)result);
            if (errList.Any())
                ret.Errors.AddRange(errList);
            return ret;
        }
        catch(Exception e)
        {
            return FluentResults.Result.Fail($"Failed to parse config resource for package {src.OwnerPackage}");
        }
    }

    public async Task<ImmutableArray<Result<IReadOnlyList<IConfigInfo>>>> TryParseResourcesAsync(IEnumerable<IConfigResourceInfo> sources)
    {
        var results = new ConcurrentQueue<Result<IReadOnlyList<IConfigInfo>>>();

        var src = sources.ToImmutableArray();
        if (!src.Any())
            return ImmutableArray<Result<IReadOnlyList<IConfigInfo>>>.Empty;

        await src.ParallelForEachAsync(async cfg =>
        {
            var res = await TryParseResourceAsync(cfg);
            results.Enqueue(res);
        }, 2);  // we only need 2 parallels to buffer against disk loading.
        
        return results.ToImmutableArray();
    }

    public async Task<Result<IReadOnlyList<IConfigProfileInfo>>> TryParseResourceAsync(IConfigProfileResourceInfo src)
    {
        if (src?.OwnerPackage is null || src.FilePaths.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(TryParseResourceAsync)}: Profile resource and/or components were null.");
        
        try
        {
            var infos = await _storageService.LoadPackageXmlFilesAsync(src.OwnerPackage, src.FilePaths);
            if (infos.IsDefaultOrEmpty)
                return FluentResults.Result.Fail($"{nameof(TryParseResourceAsync)}: No resources found.");

            var errList = new List<IError>();

            var resList = infos.Select(info =>
                {
                    if (info.Item2.Errors.Any())
                        errList.AddRange(info.Item2.Errors);
                    if (info.Item2.IsFailed || info.Item2.Value is not { } configXDoc)
                    {
                        errList.Add(new Error($"Unable to parse file: {info.Item1}"));
                        return null;
                    }

                    return configXDoc;
                })
                .Where(doc => doc != null)
                .SelectMany(doc => doc.Root.GetChildElements("Configuration"))
                .SelectMany(cfgContainer => cfgContainer.GetChildElements("Profiles"))
                .SelectMany(cfgContainer => cfgContainer.GetChildElements("Profile"))
                .Select(cfgElement =>
                {
                    try
                    {
                        return new ConfigProfileInfo()
                        {
                            OwnerPackage = src.OwnerPackage,
                            InternalName = cfgElement.GetAttributeString("Name", null),
                            ProfileValues = cfgElement.GetChildElements("ConfigValue")
                                .Select<XElement, (string ConfigName, OneOf.OneOf<string, XElement> Value)>(element =>
                                {
                                    if (element.GetAttributeString("Name", null) is not { } name)
                                        return default;
                                    if (element.GetAttributeString("Value", null) is { } value)
                                        return (name, value);
                                    if (element.GetChildElement("Value") is { } xValue)
                                        return (name, xValue);
                                    return default;
                                })
                                .Where(val => val.ConfigName is not null && val.Value.Match<bool>(
                                    s => !s.IsNullOrWhiteSpace(),
                                    element => element is not null))
                                .ToList()
                        };
                    }
                    catch (Exception e)
                    {
                        errList.Add(new Error($"Failed to parse profile var for package {src.OwnerPackage}"));
                        errList.Add(new ExceptionalError(e));
                        return null;
                    }
                })
                .Where(cfgInfo => cfgInfo != null && !cfgInfo.InternalName.IsNullOrWhiteSpace())
                .ToImmutableArray();

            var ret = FluentResults.Result.Ok((IReadOnlyList<IConfigProfileInfo>)resList);
            if (errList.Any())
                ret.Errors.AddRange(errList);
            return ret;
        }
        catch(Exception e)
        {
            return FluentResults.Result.Fail($"Failed to parse profile resource for package {src.OwnerPackage}");
        }
    }

    public async Task<ImmutableArray<Result<IReadOnlyList<IConfigProfileInfo>>>> TryParseResourcesAsync(IEnumerable<IConfigProfileResourceInfo> sources)
    {
        var results = new ConcurrentQueue<Result<IReadOnlyList<IConfigProfileInfo>>>();

        var src = sources.ToImmutableArray();
        if (!src.Any())
            return ImmutableArray<Result<IReadOnlyList<IConfigProfileInfo>>>.Empty;

        await src.ParallelForEachAsync(async cfg =>
        {
            var res = await TryParseResourceAsync(cfg);
            results.Enqueue(res);
        }, 2);  // we only need 2 parallels to buffer against disk loading.
        
        return results.ToImmutableArray();
    }
    
    private static readonly Regex RemoveInvalidChars = new Regex($"[{Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()))}]",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private string SanitizedFileName(string fileName, string replacement = "_")
    {
        return RemoveInvalidChars.Replace(fileName, replacement);
    }
}
