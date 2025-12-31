using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Barotrauma.LuaCs.Services;
using Barotrauma.Steam;
using OneOf;

namespace Barotrauma.LuaCs.Data;

#region ModConfigurationInfo

public partial record ModConfigInfo : IModConfigInfo
{
    public ContentPackage Package { get; init; }
    public string PackageName { get; init; }
    public ImmutableArray<IAssemblyResourceInfo> Assemblies { get; init; }
    public ImmutableArray<ILuaScriptResourceInfo> LuaScripts { get; init; }
    public ImmutableArray<IConfigResourceInfo> Configs { get; init; }
    public ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles { get; init; }
}

#endregion

#region DataContracts_Resources

public record AssemblyResourcesInfo(ImmutableArray<IAssemblyResourceInfo> Assemblies) : IAssembliesResourcesInfo;
public record LuaScriptsResourcesInfo(ImmutableArray<ILuaScriptResourceInfo> LuaScripts) : ILuaScriptsResourcesInfo;
public record ConfigResourcesInfo(ImmutableArray<IConfigResourceInfo> Configs) : IConfigsResourcesInfo;
public record ConfigProfilesResourcesInfo(ImmutableArray<IConfigProfileResourceInfo> ConfigProfiles) : IConfigProfilesResourcesInfo;

public record BaseResourceInfo : IBaseResourceInfo
{
    public Platform SupportedPlatforms { get; init; }
    public Target SupportedTargets { get; init; }
    public int LoadPriority { get; init; }
    public ImmutableArray<ContentPath> FilePaths { get; init; }
    public bool Optional { get; init; }
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
}

public record AssemblyResourceInfo : BaseResourceInfo, IAssemblyResourceInfo 
{
    public string FriendlyName { get; init; }
    public bool IsScript { get; init; }
}

public record ConfigResourceInfo : BaseResourceInfo, IConfigResourceInfo {}

public record ConfigProfileResourceInfo : BaseResourceInfo, IConfigProfileResourceInfo {}

public record LuaScriptsResourceInfo : BaseResourceInfo, ILuaScriptResourceInfo
{
    public bool IsAutorun { get; init; }
}

#endregion

#region DataContracts_ParsedInfo

public record ConfigInfo : IConfigInfo
{
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    public Type DataType { get; init; }
    public OneOf<string, XElement> DefaultValue { get; init; }
    public OneOf<string, XElement> Value { get; init; }
    public RunState EditableStates { get; init; }
    public NetSync NetSync { get; init; }
    
#if CLIENT // IConfigDisplayInfo
    public string DisplayName { get; init; }
    public string Description { get; init; }
    public string DisplayCategory { get; init; }
    public bool ShowInMenus { get; init; }
    public string Tooltip { get; init; }
    public string ImageIconPath { get; init; }
#endif
}

public record ConfigProfileInfo : IConfigProfileInfo
{
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    public IReadOnlyList<(string ConfigName, OneOf<string, XElement> Value)> ProfileValues { get; init; }
}

#endregion
