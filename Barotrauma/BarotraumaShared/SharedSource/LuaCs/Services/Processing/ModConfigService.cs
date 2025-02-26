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

public partial class ModConfigService : IConverterServiceAsync<ContentPackage, IModConfigInfo>, IConverterService<ContentPackage, IModConfigInfo>
{
    private readonly IStorageService _storageService;
    private readonly Lazy<IPackageManagementService> _packageManagementService;
    private int _isDisposed;
    
    private const string ModConfigFileName = "ModConfig.xml";
    private const string ModConfigRootName = "ModConfig";

    public ModConfigService(IStorageService storageService, Lazy<IPackageManagementService> pms)
    {
        _storageService = storageService;
        _packageManagementService = pms;
    }
    
    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed); 
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    public async Task<Result<IModConfigInfo>> TryParseResourceAsync(ContentPackage src)
    {
        ((IService)this).CheckDisposed();
        
        // validate package
        if (src is null)
            return FluentResults.Result.Fail<IModConfigInfo>("ContentPackage is null");
        if (_storageService.DirectoryExists(src.Path) is { } res  && (res.IsFailed || !res.Value))
            return FluentResults.Result.Fail<IModConfigInfo>($"ContentPackage does not exist or cannot be accessed: {src.Path}");
        
        // find ModConfig.xml or deep scan on fail (legacy)
        if (await _storageService.LoadPackageXmlAsync(src, ModConfigFileName) is
            { IsSuccess: true, Value: var modConfigXml }
            && modConfigXml.Root is { Name.LocalName: ModConfigRootName } root)
        {
            return await GetModConfigInfoAsync(src, root);
        }
        
        // legacy mode
        try
        {
            // we only supported assemblies and lua scripts
            var asm = GetAssembliesLegacy(src);
            var lua = GetLuaScriptsLegacy(src);

            return new ModConfigInfo()
            {
                Assemblies = asm,
                LuaScripts = lua,
                Configs = ImmutableArray<IConfigResourceInfo>.Empty,
                ConfigProfiles = ImmutableArray<IConfigProfileResourceInfo>.Empty,
                Localizations = ImmutableArray<ILocalizationResourceInfo>.Empty,
                Package = src,
                PackageName = src.Name
#if CLIENT
                ,Styles = ImmutableArray<IStylesResourceInfo>.Empty
#endif
            };
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail<IModConfigInfo>($"Unable to parse legacy content package: {src.Name}: {src.Path}");
        }
    }
    
    private partial Task<Result<IModConfigInfo>> GetModConfigInfoAsync(ContentPackage package, XElement root);
    
    private ImmutableArray<ILocalizationResourceInfo> GetLocalizations(ContentPackage src, IEnumerable<XElement> elements)
    {
        var builder = ImmutableArray.CreateBuilder<ILocalizationResourceInfo>();
        
        if (GetXmlFilesList(src, elements, "Localizations")
            is not { IsSuccess: true, Value: { } xmlFiles }) 
            return ImmutableArray<ILocalizationResourceInfo>.Empty;
        
        foreach (var file in xmlFiles)
        {
            // get dependencies
            var deps = GetElementsDependenciesData(file.Item1, src);
            // get platform, culture and target architecture
            var info = GetElementsAttributesData(file.Item1, file.Item2.First());
            
            builder.Add(new LocalizationResourceInfo()
            {
                Dependencies = deps,
                Optional = info.IsOptional,
                FilePaths = file.Item2,
                InternalName = info.Name,
                LoadPriority = info.LoadPriority,
                OwnerPackage = src,
                SupportedCultures = info.SupportedCultures,
                SupportedPlatforms = info.SupportedPlatforms,
                SupportedTargets = info.SupportedTargets
            });
        }
        
        return builder.Count > 0 
            ? builder.ToImmutable()
            : ImmutableArray<ILocalizationResourceInfo>.Empty;
    }
        
    private ImmutableArray<IAssemblyResourceInfo> GetAssemblies(ContentPackage src, IEnumerable<XElement> elements)
    {
        var builder = ImmutableArray.CreateBuilder<IAssemblyResourceInfo>();
        var elementsList = elements.ToImmutableArray();
        
        if (GetFilesList(src, elementsList, "Assembly", "*.dll")
            is not { IsSuccess: true, Value: { } xmlFiles }) 
            return ImmutableArray<IAssemblyResourceInfo>.Empty;
        
        foreach (var file in xmlFiles)
        {
            // get dependencies
            var deps = GetElementsDependenciesData(file.Item1, src);
            // get platform, culture and target architecture
            var info = GetElementsAttributesData(file.Item1, file.Item2.First());
            
            builder.Add(new AssemblyResourceInfo()
            {
                Dependencies = deps,
                Optional = info.IsOptional,
                FilePaths = file.Item2,
                InternalName = info.Name,
                LoadPriority = info.LoadPriority,
                OwnerPackage = src,
                SupportedCultures = info.SupportedCultures,
                SupportedPlatforms = info.SupportedPlatforms,
                SupportedTargets = info.SupportedTargets,
                FriendlyName = file.Item1.GetAttributeString("Name", info.Name),
                IsScript = false,
                LazyLoad = !file.Item1.GetAttributeBool("RunFile", true)
            });
        }
        
        if (GetFilesList(src, elementsList, "Assembly", "*.cs")
            is not { IsSuccess: true, Value: { } xmlFiles2 }) 
            return ImmutableArray<IAssemblyResourceInfo>.Empty;
        
        foreach (var file in xmlFiles2)
        {
            // get dependencies
            var deps = GetElementsDependenciesData(file.Item1, src);
            // get platform, culture and target architecture
            var info = GetElementsAttributesData(file.Item1, file.Item2.First());
            
            builder.Add(new AssemblyResourceInfo()
            {
                Dependencies = deps,
                Optional = info.IsOptional,
                FilePaths = file.Item2,
                InternalName = info.Name,
                LoadPriority = info.LoadPriority,
                OwnerPackage = src,
                SupportedCultures = info.SupportedCultures,
                SupportedPlatforms = info.SupportedPlatforms,
                SupportedTargets = info.SupportedTargets,
                FriendlyName = file.Item1.GetAttributeString("Name", info.Name),
                IsScript = true,
                LazyLoad = !file.Item1.GetAttributeBool("RunFile", true)
            });
        }
        
        return builder.Count > 0 
            ? builder.ToImmutable()
            : ImmutableArray<IAssemblyResourceInfo>.Empty;
    }
    
    private ImmutableArray<IConfigResourceInfo> GetConfigs(ContentPackage src, IEnumerable<XElement> elements)
    {
        var builder = ImmutableArray.CreateBuilder<IConfigResourceInfo>();
        if (GetXmlFilesList(src, elements, "Config")
            is not { IsSuccess: true, Value: { } xmlFiles }) 
            return ImmutableArray<IConfigResourceInfo>.Empty;
        
        foreach (var file in xmlFiles)
        {
            // get dependencies
            var deps = GetElementsDependenciesData(file.Item1, src);
            // get platform, culture and target architecture
            var info = GetElementsAttributesData(file.Item1, file.Item2.First());
            
            builder.Add(new ConfigResourceInfo()
            {
                Dependencies = deps,
                Optional = info.IsOptional,
                FilePaths = file.Item2,
                InternalName = info.Name,
                LoadPriority = info.LoadPriority,
                OwnerPackage = src,
                SupportedCultures = info.SupportedCultures,
                SupportedPlatforms = info.SupportedPlatforms,
                SupportedTargets = info.SupportedTargets
            });
        }
        
        return builder.Count > 0 
            ? builder.ToImmutable()
            : ImmutableArray<IConfigResourceInfo>.Empty;
    }
    
    private ImmutableArray<IConfigProfileResourceInfo> GetConfigProfiles(ContentPackage src, IEnumerable<XElement> elements)
    {
        var builder = ImmutableArray.CreateBuilder<IConfigProfileResourceInfo>();
        if (GetXmlFilesList(src, elements, "Config")
            is not { IsSuccess: true, Value: { } xmlFiles }) 
            return ImmutableArray<IConfigProfileResourceInfo>.Empty;
        
        foreach (var file in xmlFiles)
        {
            // get dependencies
            var deps = GetElementsDependenciesData(file.Item1, src);
            // get platform, culture and target architecture
            var info = GetElementsAttributesData(file.Item1, file.Item2.First());
            
            builder.Add(new ConfigProfileResourceInfo()
            {
                Dependencies = deps,
                Optional = info.IsOptional,
                FilePaths = file.Item2,
                InternalName = info.Name,
                LoadPriority = info.LoadPriority,
                OwnerPackage = src,
                SupportedCultures = info.SupportedCultures,
                SupportedPlatforms = info.SupportedPlatforms,
                SupportedTargets = info.SupportedTargets
            });
        }
        
        return builder.Count > 0 
            ? builder.ToImmutable()
            : ImmutableArray<IConfigProfileResourceInfo>.Empty;
    }
    
    private ImmutableArray<ILuaScriptResourceInfo> GetLuaScripts(ContentPackage src, IEnumerable<XElement> elements)
    {
        var builder = ImmutableArray.CreateBuilder<ILuaScriptResourceInfo>();
        if (GetXmlFilesList(src, elements, "Config")
            is not { IsSuccess: true, Value: { } xmlFiles }) 
            return ImmutableArray<ILuaScriptResourceInfo>.Empty;
        
        foreach (var file in xmlFiles)
        {
            // get dependencies
            var deps = GetElementsDependenciesData(file.Item1, src);
            // get platform, culture and target architecture
            var info = GetElementsAttributesData(file.Item1, file.Item2.First());
            
            builder.Add(new LuaScriptScriptResourceInfo()
            {
                Dependencies = deps,
                Optional = info.IsOptional,
                FilePaths = file.Item2,
                InternalName = info.Name,
                LoadPriority = info.LoadPriority,
                OwnerPackage = src,
                SupportedCultures = info.SupportedCultures,
                SupportedPlatforms = info.SupportedPlatforms,
                SupportedTargets = info.SupportedTargets,
                IsAutorun = file.Item1.GetAttributeBool("RunFile", true)
            });
        }
        
        return builder.Count > 0 
            ? builder.ToImmutable()
            : ImmutableArray<ILuaScriptResourceInfo>.Empty;
    }

    private Result<ImmutableArray<(XElement, ImmutableArray<string>)>> GetXmlFilesList(ContentPackage src,
        IEnumerable<XElement> elements, string elementNameCheck) =>
        GetFilesList(src, elements, elementNameCheck, "*.xml");
    
    private Result<ImmutableArray<(XElement, ImmutableArray<string>)>> GetFilesList(ContentPackage src,
        IEnumerable<XElement> elements, string elementNameCheck, string filter)
    {
        var builder = ImmutableArray.CreateBuilder<(XElement, ImmutableArray<string>)>();
        
        if (elementNameCheck.IsNullOrWhiteSpace())
            throw new ArgumentNullException($"{nameof(GetXmlFilesList)}: The element check is null.");
        
        foreach (var element in elements)
        {
            if (element.Name.LocalName != elementNameCheck)
                throw new ArgumentException("Element is not a Localization element");
            
            if (element.GetAttributeString("Folder", string.Empty) is { } str 
                && !string.IsNullOrWhiteSpace(str))
            {
                if (_storageService.FindFilesInPackage(src, str, filter, true) 
                        is not { IsSuccess: true, Value: var fpList } || !fpList.Any())
                {
                    continue;
                }

                foreach (var fileP in fpList)
                    builder.Add((element, fpList.ToImmutableArray()));
            }
            else if (element.GetAttributeString("File", string.Empty) is { } fileStr 
                     && !string.IsNullOrWhiteSpace(fileStr) 
                     && _storageService.GetAbsFromPackage(src, fileStr) is { IsSuccess: true, Value: var fp } 
                     && _storageService.FileExists(fp) is { IsSuccess: true, Value: true })
            {
                builder.Add((element, new [] { fileStr }.ToImmutableArray()));
            }
        }

        return builder.Count > 0
            ? FluentResults.Result.Ok(builder.ToImmutable())
            : FluentResults.Result.Fail($"No files found");
    }

    private ResourceAdditionalInfo GetElementsAttributesData(XElement element, string localPath)
    {
        return new ResourceAdditionalInfo(
            element.GetAttributeString("Name", localPath),
            GetSupportedPlatforms(element.GetAttributeString("Platform", "any")),
            GetSupportedTargets(element.GetAttributeString("Target", "any")),
            GetSupportedCultures(element),
            element.GetAttributeBool("Optional", false),
            element.GetAttributeInt("Priority", 0));
        
        Platform GetSupportedPlatforms(string platformName) => platformName.ToLowerInvariant().Trim() switch
        {
            "windows" => Platform.Windows,
            "linux" => Platform.Linux,
            "osx" => Platform.OSX,
            _ => Platform.Windows | Platform.Linux | Platform.OSX
        };
        
        Target GetSupportedTargets(string targetName) => targetName.ToLowerInvariant().Trim() switch
        {
            "client" => Target.Client,
            "server" => Target.Server,
            _ => Target.Client | Target.Server,
        };

        ImmutableArray<CultureInfo> GetSupportedCultures(XElement element)
        {
            var culture = element.GetAttributeString("Culture", string.Empty);
            if (string.IsNullOrWhiteSpace(culture))
                return new[] { CultureInfo.InvariantCulture }.ToImmutableArray();
            var builder = ImmutableArray.CreateBuilder<CultureInfo>();
            var arr = culture.Split(',');
            if (arr.Length == 0)
                return new[] { CultureInfo.InvariantCulture }.ToImmutableArray();
            foreach (var culstr in arr)
            {
                if (string.IsNullOrWhiteSpace(culstr))
                    continue;
                try
                {
                    builder.Add(
                        culstr.ToLowerInvariant().Trim() == "default" 
                            ? CultureInfo.InvariantCulture 
                            : CultureInfo.GetCultureInfo(culstr));
                }
                catch (CultureNotFoundException e)
                {
                    // This is the case if a culture is specified by the package that is not supported by the OS/.NET ENV.
                    // We ignore it since we can never use it.
                    continue;
                }
            }
            
            return builder.Count > 0
                ? builder.ToImmutable()
                : new[] { CultureInfo.InvariantCulture }.ToImmutableArray();
        }
    }

    private ImmutableArray<IPackageDependency> GetElementsDependenciesData(XElement element, ContentPackage src)
    {
        if (element.GetChildElement("Dependencies") is not {} dependencies
            || dependencies.GetChildElements("Dependency").ToImmutableArray() is not { Length: >0 } depsList)
            return ImmutableArray<IPackageDependency>.Empty;
        var builder = ImmutableArray.CreateBuilder<IPackageDependency>();
        foreach (var dep in depsList)
        {
            var packName = dep.GetAttributeString("PackageName", string.Empty);
            var packId = dep.GetAttributeUInt64("PackageId", 0);
            
            // invalid entry
            if (packName.IsNullOrWhiteSpace() && packId == 0)
                continue;

            if (_packageManagementService.Value.GetPackageDependencyInfo(src, packName, packId) is
                { IsSuccess: true, Value: { } depsInfo })
            {
                builder.Add(depsInfo);
            }
        }
        return builder.ToImmutable();
    }
    
    private ImmutableArray<IAssemblyResourceInfo> GetAssembliesLegacy(ContentPackage src)
        {
            var builder = ImmutableArray.CreateBuilder<IAssemblyResourceInfo>();
            // server, linux
            if (_storageService.FindFilesInPackage(src, "bin/Server/Linux", "*.dll", true) 
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false} filesSrvLin})
            {
                builder.Add(new AssemblyResourceInfo()
                {
                    Dependencies = ImmutableArray<IPackageDependency>.Empty,
                    FilePaths = filesSrvLin,
                    FriendlyName = "AssembliesServerLinux",
                    InternalName = "AssembliesServerLinux",
                    IsScript = false,
                    LazyLoad = false,
                    LoadPriority = 1,
                    Optional = false,
                    OwnerPackage = src,
                    SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                    SupportedPlatforms = Platform.Linux,
                    SupportedTargets = Target.Server
                });
            }
            
            // server, osx
            if (_storageService.FindFilesInPackage(src, "bin/Server/OSX", "*.dll", true) 
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false} filesSrvOsx})
            {
                builder.Add(new AssemblyResourceInfo()
                {
                    Dependencies = ImmutableArray<IPackageDependency>.Empty,
                    FilePaths = filesSrvOsx,
                    FriendlyName = "AssembliesServerOSX",
                    InternalName = "AssembliesServerOSX",
                    IsScript = false,
                    LazyLoad = false,
                    LoadPriority = 1,
                    Optional = false,
                    OwnerPackage = src,
                    SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                    SupportedPlatforms = Platform.OSX,
                    SupportedTargets = Target.Server
                });
            }
            
            // server, osx
            if (_storageService.FindFilesInPackage(src, "bin/Server/Windows", "*.dll", true) 
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false} filesSrvWin})
            {
                builder.Add(new AssemblyResourceInfo()
                {
                    Dependencies = ImmutableArray<IPackageDependency>.Empty,
                    FilePaths = filesSrvWin,
                    FriendlyName = "AssembliesServerWin",
                    InternalName = "AssembliesServerWin",
                    IsScript = false,
                    LazyLoad = false,
                    LoadPriority = 1,
                    Optional = false,
                    OwnerPackage = src,
                    SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                    SupportedPlatforms = Platform.Windows,
                    SupportedTargets = Target.Server
                });
            }
            
            // client, linux
            if (_storageService.FindFilesInPackage(src, "bin/Client/Linux", "*.dll", true) 
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false} filesCliLin})
            {
                builder.Add(new AssemblyResourceInfo()
                {
                    Dependencies = ImmutableArray<IPackageDependency>.Empty,
                    FilePaths = filesCliLin,
                    FriendlyName = "AssembliesClientLinux",
                    InternalName = "AssembliesClientLinux",
                    IsScript = false,
                    LazyLoad = false,
                    LoadPriority = 1,
                    Optional = false,
                    OwnerPackage = src,
                    SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                    SupportedPlatforms = Platform.Linux,
                    SupportedTargets = Target.Client
                });
            }
            
            // server, osx
            if (_storageService.FindFilesInPackage(src, "bin/Client/OSX", "*.dll", true) 
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false} filesCliOsx})
            {
                builder.Add(new AssemblyResourceInfo()
                {
                    Dependencies = ImmutableArray<IPackageDependency>.Empty,
                    FilePaths = filesCliOsx,
                    FriendlyName = "AssembliesClientOSX",
                    InternalName = "AssembliesClientOSX",
                    IsScript = false,
                    LazyLoad = false,
                    LoadPriority = 1,
                    Optional = false,
                    OwnerPackage = src,
                    SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                    SupportedPlatforms = Platform.OSX,
                    SupportedTargets = Target.Client
                });
            }
            
            // server, osx
            if (_storageService.FindFilesInPackage(src, "bin/Client/Windows", "*.dll", true) 
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false} filesCliWin})
            {
                builder.Add(new AssemblyResourceInfo()
                {
                    Dependencies = ImmutableArray<IPackageDependency>.Empty,
                    FilePaths = filesCliWin,
                    FriendlyName = "AssembliesClientWin",
                    InternalName = "AssembliesClientWin",
                    IsScript = false,
                    LazyLoad = false,
                    LoadPriority = 1,
                    Optional = false,
                    OwnerPackage = src,
                    SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                    SupportedPlatforms = Platform.Windows,
                    SupportedTargets = Target.Client
                });
            }
            
            var sharedFound = _storageService.FindFilesInPackage(src, "CSharp/Shared", "*.cs", true)
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false } filesCssShared };
            
            // source files legacy: server
            if (_storageService.FindFilesInPackage(src, "CSharp/Server", "*.cs", true)
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false} filesCssServer})
            {
                builder.Add(new AssemblyResourceInfo()
                {
                    Dependencies = ImmutableArray<IPackageDependency>.Empty,
                    FilePaths = sharedFound ? filesCssServer.Concat(filesCssShared).ToImmutableArray() : filesCssServer,
                    FriendlyName = "CssServer",
                    InternalName = "CssServer",
                    IsScript = true,
                    LazyLoad = false,
                    LoadPriority = 1,
                    Optional = false,
                    OwnerPackage = src,
                    SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                    SupportedPlatforms = Platform.Linux | Platform.OSX | Platform.Windows,
                    SupportedTargets = Target.Server
                });
            }

            // source files legacy: client
            if (_storageService.FindFilesInPackage(src, "CSharp/Client", "*.cs", true)
                is { IsSuccess: true, Value: { IsDefaultOrEmpty: false} filesCssClient})
            {
                builder.Add(new AssemblyResourceInfo()
                {
                    Dependencies = ImmutableArray<IPackageDependency>.Empty,
                    FilePaths = sharedFound ? filesCssClient.Concat(filesCssShared).ToImmutableArray() : filesCssClient,
                    FriendlyName = "CssClient",
                    InternalName = "CssClient",
                    IsScript = true,
                    LazyLoad = false,
                    LoadPriority = 1,
                    Optional = false,
                    OwnerPackage = src,
                    SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                    SupportedPlatforms = Platform.Linux | Platform.OSX | Platform.Windows,
                    SupportedTargets = Target.Client
                });
            }

            return builder.MoveToImmutable();
        }
    private ImmutableArray<ILuaScriptResourceInfo> GetLuaScriptsLegacy(ContentPackage src)
    {
        var builder = ImmutableArray.CreateBuilder<ILuaScriptResourceInfo>();
        
        if (_storageService.FindFilesInPackage(src, "Lua", "*.lua", true)
            is { IsSuccess: true, Value: { IsDefaultOrEmpty: false } fileAll })
        {
            builder.Add(new LuaScriptScriptResourceInfo()
            {
                Dependencies = ImmutableArray<IPackageDependency>.Empty,
                FilePaths = fileAll.Where(path => !path.Contains("Autorun")).ToImmutableArray(),
                InternalName = "LuaScriptsNormal",
                Optional = false,
                IsAutorun = false,
                OwnerPackage = src,
                SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                SupportedPlatforms = Platform.Linux | Platform.OSX | Platform.Windows,
                SupportedTargets = Target.Client | Target.Server
            });
            
            builder.Add(new LuaScriptScriptResourceInfo()
            {
                Dependencies = ImmutableArray<IPackageDependency>.Empty,
                FilePaths = fileAll.Where(path => path.Contains("Autorun")).ToImmutableArray(),
                InternalName = "LuaScriptsAutorun",
                Optional = false,
                IsAutorun = true,
                OwnerPackage = src,
                SupportedCultures = new CultureInfo[]{ CultureInfo.InvariantCulture }.ToImmutableArray(),
                SupportedPlatforms = Platform.Linux | Platform.OSX | Platform.Windows,
                SupportedTargets = Target.Client | Target.Server
            });
        }
        
        return builder.MoveToImmutable();
    }

    public async Task<ImmutableArray<Result<IModConfigInfo>>> TryParseResourcesAsync(IEnumerable<ContentPackage> sources)
    {
        ((IService)this).CheckDisposed();
        
        var srcs = sources.ToImmutableArray();
        var results = new AsyncLocal<ConcurrentQueue<Result<IModConfigInfo>>>();
        await srcs.ParallelForEachAsync(async pkg =>
        {
            try
            {
                results.Value.Enqueue(await TryParseResourceAsync(pkg));
            }
            catch (Exception e)
            {
                // this should never happen but this is to stop partial execution exit.
                results.Value.Enqueue(
                    FluentResults.Result.Fail<IModConfigInfo>($"Failed to parse package {pkg?.Name}: {e.Message}"));
            }
        });
        return results.Value.ToImmutableArray();
    }

    public Result<IModConfigInfo> TryParseResource(ContentPackage src) => 
        TryParseResourceAsync(src).GetAwaiter().GetResult();
    public ImmutableArray<Result<IModConfigInfo>> TryParseResources(IEnumerable<ContentPackage> sources) => 
        TryParseResourcesAsync(sources.ToImmutableArray()).GetAwaiter().GetResult();

    private record ResourceAdditionalInfo(
        string Name,
        Platform SupportedPlatforms,
        Target SupportedTargets,
        ImmutableArray<CultureInfo> SupportedCultures,
        bool IsOptional,
        int LoadPriority);
}
