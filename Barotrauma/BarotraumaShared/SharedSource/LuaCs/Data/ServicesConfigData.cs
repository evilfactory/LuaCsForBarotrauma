using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using Barotrauma.LuaCs;
using Barotrauma.Networking;
using FluentResults;
using OneOf.Types;

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
    string LocalDataSavePath { get; }
    string LocalDataPathRegex { get; }
    string LocalPackageDataPath { get; }
}

public record StorageServiceConfig : IStorageServiceConfig
{
    private static readonly string ExecutionLocation = Directory.GetCurrentDirectory().CleanUpPathCrossPlatform();

    public string LocalModsDirectory { get; init; } = System.IO.Path.GetFullPath(ContentPackage.LocalModsDir).CleanUpPath();
    public string WorkshopModsDirectory { get; init; } = System.IO.Path.GetFullPath(ContentPackage.WorkshopModsDir).CleanUpPath();
    public string GameSettingsConfigPath { get; init; } = System.IO.Path.GetFullPath(
        string.IsNullOrEmpty(GameSettings.CurrentConfig.SavePath)
            ? SaveUtil.DefaultSaveFolder
            : GameSettings.CurrentConfig.SavePath).CleanUpPath();
#if CLIENT
    public string TempDownloadsDirectory { get; init; } = System.IO.Path.GetFullPath(ModReceiver.DownloadFolder).CleanUpPath();
#endif
    public string LocalDataSavePath => Path.Combine(ExecutionLocation, "Data/Mods").CleanUpPathCrossPlatform();
    public string LocalDataPathRegex => "%ModDir%";
    public string RunLocation => ExecutionLocation;

    public string LocalPackageDataPath => Path.Combine(LocalDataSavePath, LocalDataPathRegex);

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

// --- Package Management Service
public interface IPackageManagementServiceConfig : IService
{
    bool IsCsEnabled { get; }
}

public class PackageManagementServiceConfig : IPackageManagementServiceConfig
{
    public void Dispose()
    {
        // ignored
    }

    public bool IsDisposed => false;
    public bool IsCsEnabled =>  true;
}
