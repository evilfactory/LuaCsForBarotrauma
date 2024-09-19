using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Barotrauma.LuaCs;

public readonly struct ModConfigData
{
    public bool RunConfigLegacy { get; init; }
    
    public Dependency[] Dependencies { get; init; }
    public AssemblyResource[] LoadableAssemblies { get; init; }


    public readonly struct ConfigInitData
    {
        // TODO: complete struct, data here should be already parsed and ready-to-use by the config service.    
    }
    
    public readonly struct AssemblyResource
    {
        /// <summary>
        /// The friendly name of the assembly. Script files belonging to the same assembly should all have the same name.
        /// Legacy scripts will all be given the sanitized name of the Content Package they belong to.
        /// </summary>
        public string Name { get; init; }
        /// <summary>
        /// Is this entry referring to a script file.
        /// </summary>
        public bool IsScriptFile { get; init; }
        /// <summary>
        /// Should this be compiled or loaded immediately or stored for On-Demand compilation.
        /// </summary>
        public bool LazyLoad { get; init; }
        /// <summary>
        /// Path to the file.
        /// </summary>
        public string Path { get; init; }
        /// <summary>
        /// All supported platforms.
        /// </summary>
        public Platform Platforms { get; init; }
        /// <summary>
        /// All supported run targets (client and/or server)
        /// </summary>
        public Target Targets { get; init; }
    }
    
    public readonly struct Dependency
    {
        /// <summary>
        /// Root folder of the content package.
        /// </summary>
        public string FolderPath { get; init; }
        /// <summary>
        /// Name of the package.
        /// </summary>
        public string PackageName { get; init; }
        /// <summary>
        /// Steam ID of the package. 
        /// </summary>
        public int SteamId { get; init; }
        /// <summary>
        /// The dependency package, if found.
        /// </summary>
        public ContentPackage DependencyPackage { get; init; }
    }
    
    [Flags]
    public enum Platform
    {
        Linux=0x1, 
        OSX=0x2, 
        Windows=0x4
    }
    
    [Flags]
    public enum Target
    {
        Client=0x1, 
        Server=0x2
    }
}
