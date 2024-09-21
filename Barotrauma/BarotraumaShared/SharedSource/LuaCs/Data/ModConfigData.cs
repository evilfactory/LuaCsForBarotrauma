using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Data;

public readonly struct ModConfigData
{
    public bool RunConfigLegacy { get; init; }
    
    public DependencyInfo[] Dependencies { get; init; }
    public AssemblyResourceInfo[] LoadableAssemblies { get; init; }


    public readonly struct ConfigInitData
    {
        // TODO: complete struct, data here should be already parsed and ready-to-use by the config service.    
    }
    
    public readonly struct AssemblyResourceInfo : IAssemblyResourceInfo
    {
        public string Name { get; init; }
        public bool IsScriptFile { get; init; }
        public bool LazyLoad { get; init; }
        public string Path { get; init; }
        public Platform Platforms { get; init; }
        public Target Targets { get; init; }
    }
    
    public readonly struct DependencyInfo : IPackageDependencyInfo
    {
        public string FolderPath { get; init; }
        public string PackageName { get; init; }
        public int SteamId { get; init; }
        public ContentPackage DependencyPackage { get; init; }
    }
    
    
}
