using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using Barotrauma.Networking;
using FluentResults;

namespace Barotrauma.LuaCs.Data;


// --- Storage Service
public interface IStorageServiceConfig
{
    string LocalModsDirectory { get; }
    string WorkshopModsDirectory { get; }
    string GameSettingsConfigPath { get; }
#if CLIENT
    string TempDownloadsDirectory { get; }
#endif
    
    ReadOnlyCollection<string> SafeIOReadDirectories { get; }
    ReadOnlyCollection<string> SafeIOWriteDirectories { get; }
    
    string LocalDataSavePath { get; }
    string LocalDataPathRegex { get; }
    string LocalPackageDataPath { get; }
    public string RunLocation { get; }
    bool GlobalSafeIOEnabled { get; }
}

internal interface IStorageServiceConfigUpdate
{
    public FluentResults.Result SetSafeReadDirectories(string[] directories);
    public FluentResults.Result SetSafeWriteDirectories(string[] directories);
}

public record StorageServiceConfig : IStorageServiceConfig, IStorageServiceConfigUpdate
{
    private static readonly string ExecutionLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location.CleanUpPath());

    public string LocalModsDirectory { get; init; } = System.IO.Path.GetFullPath(ContentPackage.LocalModsDir).CleanUpPath();
    public string WorkshopModsDirectory { get; init; } = System.IO.Path.GetFullPath(ContentPackage.WorkshopModsDir).CleanUpPath();
    public string GameSettingsConfigPath { get; init; } = System.IO.Path.GetFullPath(
        string.IsNullOrEmpty(GameSettings.CurrentConfig.SavePath)
            ? SaveUtil.DefaultSaveFolder
            : GameSettings.CurrentConfig.SavePath).CleanUpPath();
#if CLIENT
    public string TempDownloadsDirectory { get; init; } = System.IO.Path.GetFullPath(ModReceiver.DownloadFolder).CleanUpPath();
#endif

    private string[] _safeIOReadDirectories = Array.Empty<string>();
    public ReadOnlyCollection<string> SafeIOReadDirectories => Array.AsReadOnly(_safeIOReadDirectories);

    private string[] _safeIOWriteDirectories = Array.Empty<string>();
    public ReadOnlyCollection<string> SafeIOWriteDirectories => Array.AsReadOnly(_safeIOWriteDirectories);

    public string LocalDataSavePath => Path.Combine(ExecutionLocation, "/Data/Mods/");

    public string LocalDataPathRegex => "<PACKAGENAME>";

    public string RunLocation => ExecutionLocation;
    public bool GlobalSafeIOEnabled => false;

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

    
    public FluentResults.Result SetSafeReadDirectories(string[] directories)
    {
        return SetSafeDirectory(ref _safeIOReadDirectories, directories);
    }

    public FluentResults.Result SetSafeWriteDirectories(string[] directories)
    {
        return SetSafeDirectory(ref _safeIOWriteDirectories, directories);
    }

    private FluentResults.Result SetSafeDirectory(ref string[] target, string[] directories)
    {
        if (directories is null || directories.Length < 1)
        {
            _safeIOReadDirectories = Array.Empty<string>();
            return FluentResults.Result.Ok();
        }
        
        try
        {
            string[] dirs = new string[directories.Length];
            Array.Copy(directories, dirs, directories.Length);
            for (int i = 0; i < dirs.Length; i++)
            {
                dirs[i] = System.IO.Path.GetFullPath(dirs[i]).CleanUpPath();
            }

            target = dirs;
            return FluentResults.Result.Ok();
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(
                new ExceptionalError(e).WithMetadata(FluentResults.LuaCs.MetadataType.ExceptionObject, this));
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
    bool SafeLuaIOEnabled { get; }
    bool UseCaching { get; }
}

public record LuaScriptServicesConfig : ILuaScriptServicesConfig
{
    public bool SafeLuaIOEnabled => true;
    public bool UseCaching => true;
}
