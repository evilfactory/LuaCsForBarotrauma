using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FarseerPhysics.Common;
using FluentResults;
using OneOf;

namespace Barotrauma.LuaCs.Services.Processing;

public class ConfigIOService : IConfigIOService
{
    private readonly IStorageService _storageService;
    private readonly IConfigServiceConfig _configServiceConfig;

    public ConfigIOService(IStorageService storageService, IConfigServiceConfig configServiceConfig)
    {
        this._storageService = storageService;
        storageService.UseCaching = true;
        _configServiceConfig = configServiceConfig;
    }
    
    public void Dispose()
    {
        // stateless service
        return;
    }

    // stateless service
    public bool IsDisposed => false;
    public FluentResults.Result Reset()
    {
        _storageService.PurgeCache();
        return FluentResults.Result.Ok();
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
                .SelectMany(doc => doc.configXDoc.Root.GetChildElements("Configuration"))
                .SelectMany(cfgContainer => cfgContainer.GetChildElements("Configs"))
                .SelectMany(cfgContainer => cfgContainer.GetChildElements("Config"))
                .Select(async cfgElement =>
                {
                    try
                    {
                        OneOf.OneOf<string, XElement> defaultValue = cfgElement.GetChildElement("Value");
                        if (defaultValue.AsT1 is null)
                            defaultValue = cfgElement.GetAttributeString("Value", string.Empty);

                        var internalName = cfgElement.GetAttributeString("Name", string.Empty);
                        if (internalName.IsNullOrWhiteSpace())
                            return null;
                        
                        return new ConfigInfo()
                        {
                            DataType = Type.GetType(cfgElement.GetAttributeString("Type", "string")),
                            OwnerPackage = src.OwnerPackage,
                            DefaultValue = defaultValue,
                            Value = await LoadConfigDataFromLocal(src.OwnerPackage, internalName) is { IsSuccess: true } res 
                                ? res.Value : defaultValue,
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
                .Where(doc => doc is not null)
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
    
    public async Task<FluentResults.Result> SaveConfigDataLocal(ContentPackage package, string configName, XElement serializedValue)
    {
        if (package is null || package.Name.IsNullOrWhiteSpace() || configName.IsNullOrWhiteSpace() || serializedValue is null)
            return FluentResults.Result.Fail($"{nameof(SaveConfigDataLocal)}: Argument(s) were null");

        var res = await LoadPackageConfigDocInternal(package);

    }

    public async Task<Result<OneOf<string, XElement>>> LoadConfigDataFromLocal(ContentPackage package, string configName)
    {
        if (package is null || package.Name.IsNullOrWhiteSpace() || configName.IsNullOrWhiteSpace())
            return FluentResults.Result.Fail($"{nameof(LoadConfigDataFromLocal)}: Argument(s) were null");

        var filePath = _configServiceConfig.LocalConfigPathPartial.Replace(
            _configServiceConfig.FileNamePattern,
            $"{SanitizedFileName(package.Name)}.xml");

        var res = await _storageService.LoadLocalXmlAsync(package, filePath);
    }

    private async Task<FluentResults.Result<XDocument>> LoadPackageConfigDocInternal(ContentPackage package)
    {
        var filePath = _configServiceConfig.LocalConfigPathPartial.Replace(
            _configServiceConfig.FileNamePattern,
            $"{SanitizedFileName(package.Name)}.xml");
    }
}
