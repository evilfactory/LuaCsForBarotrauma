using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;
using FluentResults;

namespace Barotrauma.LuaCs.Data;


// --- Storage Service
// TODO: Configs should not be services, add new registration path for them.
public interface IStorageServiceConfig : IService
{
    string LocalModsDirectory { get; }
    string WorkshopModsDirectory { get; }
    string GameSettingsConfigPath { get; }
#if CLIENT
    string TempDownloadsDirectory { get; }
#endif
    
    //ReadOnlyCollection<string> SafeIOReadDirectories { get; }
    //ReadOnlyCollection<string> SafeIOWriteDirectories { get; }
    IEnumerable<string> GlobalIOReadWhitelist();
    IEnumerable<string> GlobalIOWriteWhitelist();
    
    bool IOReadWhiteListContains(string filePath);
    bool IOWriteWhiteListContains(string filePath);
    
    string LocalDataSavePath { get; }
    string LocalDataPathRegex { get; }
    string LocalPackageDataPath { get; }
    public string RunLocation { get; }
    bool GlobalSafeIOEnabled { get; }
}

internal interface IStorageServiceConfigUpdate
{
    public FluentResults.Result SetSafeReadFilePaths(string[] filePaths);
    public FluentResults.Result SetSafeWriteFilePaths(string[] filePaths);
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

    private readonly AsyncReaderWriterLock _safeIOReadLock = new();
    private readonly AsyncReaderWriterLock _safeIOWriteLock = new();
    private readonly ConcurrentDictionary<string,byte> _safeIOReadFilePaths = new();

    private readonly ConcurrentDictionary<string,byte> _safeIOWriteFilePaths = new();

    public IEnumerable<string> GlobalIOReadWhitelist()
    {
        using var lck = _safeIOReadLock.AcquireReaderLock().GetAwaiter().GetResult();

        if (_safeIOReadFilePaths.Count == 0)
        {
            yield break;
        }
        
        foreach (var path in _safeIOReadFilePaths)
        {
            yield return path.Key;
        }
    }

    public IEnumerable<string> GlobalIOWriteWhitelist()
    {
        using var lck = _safeIOWriteLock.AcquireReaderLock().GetAwaiter().GetResult();

        if (_safeIOWriteFilePaths.Count == 0)
        {
            yield break;
        }
        
        foreach (var path in _safeIOWriteFilePaths)
        {
            yield return path.Key;
        }
    }

    public bool IOReadWhiteListContains(string filePath)
    {
        if (filePath.IsNullOrWhiteSpace())
            return false;
        return _safeIOReadFilePaths.ContainsKey(filePath);
    }

    public bool IOWriteWhiteListContains(string filePath)
    {
        if (filePath.IsNullOrWhiteSpace())
            return false;
        return _safeIOWriteFilePaths.ContainsKey(filePath);
    }

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

    
    public FluentResults.Result SetSafeReadFilePaths(string[] filePaths)
    {
        using var lck = _safeIOReadLock.AcquireWriterLock().GetAwaiter().GetResult();
        return SetSafeDirectory(_safeIOReadFilePaths, filePaths);
    }

    public FluentResults.Result SetSafeWriteFilePaths(string[] filePaths)
    {
        using  var lck = _safeIOWriteLock.AcquireWriterLock().GetAwaiter().GetResult();
        return SetSafeDirectory(_safeIOWriteFilePaths, filePaths);
    }

    private FluentResults.Result SetSafeDirectory(ConcurrentDictionary<string,byte> target, string[] filePaths)
    {
        if (filePaths is null || filePaths.Length < 1)
        {
            target.Clear();
            return FluentResults.Result.Ok();
        }
        
        FluentResults.Result result = new();
            
        target.Clear();
        foreach (string path in filePaths)
        {
            if (path.IsNullOrWhiteSpace())
            {
                result = result.WithError($"ServicesConfigData: A supplied whitelist path was null.");
                continue;
            }
                
            try
            {
                var path2 = Path.GetFullPath(path);
                target.TryAdd(path2, 0);
            }
            catch (Exception e)
            {
                result = result.WithError(
                    new ExceptionalError(e).WithMetadata(FluentResults.LuaCs.MetadataType.ExceptionObject, this));
                continue;
            }
        }

        return result.WithSuccess($"Whitelist updated.");
    }

    public void Dispose()
    {
        // cannot be disposed.
    }

    public bool IsDisposed => false;
}

// --- Config Service
public interface IConfigServiceConfig : IService
{
    string LocalConfigPathPartial { get; }
    string FileNamePattern { get; }
}

public record ConfigServiceConfig : IConfigServiceConfig
{
    public string LocalConfigPathPartial => $"/Config/{FileNamePattern}.xml";
    public string FileNamePattern => "<ConfigName>";
    public void Dispose()
    {
        // ignored
    }
    public bool IsDisposed => false;
}


// --- Lua Scripts Service
public interface ILuaScriptServicesConfig : IService
{
    bool SafeLuaIOEnabled { get; }
    bool UseCaching { get; }
}

public record LuaScriptServicesConfig : ILuaScriptServicesConfig
{
    public bool SafeLuaIOEnabled => true;
    public bool UseCaching => true;
    public void Dispose()
    {
        // ignored
    }

    public bool IsDisposed => false;
}
