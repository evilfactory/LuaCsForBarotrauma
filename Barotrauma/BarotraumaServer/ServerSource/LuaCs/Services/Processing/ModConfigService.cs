using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services.Processing;

public partial class ModConfigService
{
    private partial async Task<Result<IModConfigInfo>> GetModConfigInfoAsync(ContentPackage package, XElement root)
    {
        var asm = root.GetChildElements("Assembly").ToImmutableArray();
        var loc = root.GetChildElements("Localization").ToImmutableArray();
        var cfg = root.GetChildElements("Config").ToImmutableArray();
        var lua = root.GetChildElements("Lua").ToImmutableArray();

        return FluentResults.Result.Ok<IModConfigInfo>(new ModConfigInfo()
        {
            Package = package,
            PackageName = package.Name,
            Assemblies = asm.Any() ? GetAssemblies(package, asm) : ImmutableArray<IAssemblyResourceInfo>.Empty,
            Localizations = loc.Any() ? GetLocalizations(package, loc) : ImmutableArray<ILocalizationResourceInfo>.Empty,
            Configs = cfg.Any() ? GetConfigs(package, cfg) : ImmutableArray<IConfigResourceInfo>.Empty,
            ConfigProfiles = cfg.Any() ? GetConfigProfiles(package, cfg) : ImmutableArray<IConfigProfileResourceInfo>.Empty,
            LuaScripts = lua.Any() ? GetLuaScripts(package, lua) : ImmutableArray<ILuaScriptResourceInfo>.Empty
        });
    }
}
