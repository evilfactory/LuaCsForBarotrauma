using System;
using System.IO;

namespace Barotrauma.LuaCs.Data;


// --- Storage Service
public interface IStorageServiceConfig
{
    string LocalDataPathRegex { get; }
    string LocalPackagePath { get; }
}

public record StorageServiceConfig : IStorageServiceConfig
{
    public string LocalDataPathRegex => "<PACKAGENAME>";

    public string LocalPackagePath
    {
        get
        {
            var val = GameMain.LuaCs?.LocalDataSavePath?.Value ?? "/Data/Mods/";
            return ContainsIllegalPaths(val) ? $"/Data/Mods/{LocalDataPathRegex}" : Path.Combine(val, LocalDataPathRegex);
        }
    }

    private bool ContainsIllegalPaths(string path)
    {
        throw new NotImplementedException();
    }
}

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
