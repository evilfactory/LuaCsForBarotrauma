using System;
using System.IO;
using System.Reflection;

namespace Barotrauma.LuaCs.Data;


// --- Storage Service
public interface IStorageServiceConfig
{
    string LocalDataSavePath { get; }
    string LocalDataPathRegex { get; }
    string LocalPackageDataPath { get; }
    public string RunLocation { get; }
}

public record StorageServiceConfig : IStorageServiceConfig
{
    private static readonly string ExecutionLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location.CleanUpPath());
    public string LocalDataSavePath => Path.Combine(ExecutionLocation, "/Data/Mods/");

    public string LocalDataPathRegex => "<PACKAGENAME>";

    public string RunLocation => ExecutionLocation;
    
    public string LocalPackageDataPath
    {
        get
        {
            return ContainsIllegalPaths(LocalDataSavePath) ? $"/Data/Mods/{LocalDataPathRegex}" 
                : Path.Combine(LocalDataSavePath, LocalDataPathRegex);
            
            bool ContainsIllegalPaths(string path)
            {
                throw new NotImplementedException();
            }
        }
    }
}

// --- Config Service
public interface IConfigServiceConfig
{
    string LocalConfigPathPartial { get; }
    string FileNamePattern { get; }
}

public record ConfigServiceConfig : IConfigServiceConfig
{
    public string LocalConfigPathPartial => $"/Config/{FileNamePattern}.xml";
    public string FileNamePattern => "<ConfigName>";
}


// --- Lua Scripts Service
public interface ILuaScriptServicesConfig
{
    
}

public record LuaScriptServicesConfig : ILuaScriptServicesConfig
{
    
}
