using Barotrauma.IO;
using Barotrauma.LuaCs.Data;
using Barotrauma.Networking;
using FarseerPhysics.Common;
using FluentResults;
using FluentResults.LuaCs;
using Microsoft.Toolkit.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Path = System.IO.Path;

namespace Barotrauma.LuaCs;

public class SafeStorageService : StorageService, ISafeStorageService
{
    private ConcurrentDictionary<string, byte> 
        _fileListRead = new (), 
        _fileListWrite = new();
    private readonly AsyncReaderWriterLock _higherOperationsLock = new(); 
    
    public SafeStorageService(IStorageServiceConfig configData) : base(configData)
    {
        IsReadOperationAllowedEval = (fp) => IsFileAccessible(fp, true, true);
        IsWriteOperationAllowedEval = (fp) => IsFileAccessible(fp, false, true);
    }
    
    private string GetFullPath(string path) => System.IO.Path.GetFullPath(path).CleanUpPathCrossPlatform();

    public bool IsFileAccessible(string path, bool readOnly, bool checkWhitelistOnly = true)
    {
        Guard.IsNotNullOrWhiteSpace(path,  nameof(path));
        using var lck = _higherOperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        try
        {
            path = GetFullPath(path);

            if (path.StartsWith(ConfigData.WorkshopModsDirectory)
                || path.StartsWith(ConfigData.LocalModsDirectory)
#if CLIENT
                || path.StartsWith(ConfigData.TempDownloadsDirectory)
#endif
                )
            {
                return true;
            }

            if (!_fileListRead.ContainsKey(path))
            {
                return false;
            }
            if (!readOnly && !_fileListWrite.ContainsKey(path))
            {
                return false;
            }
            if (checkWhitelistOnly)
            {
                return true;
            }
            using var fs = System.IO.File.Open(
                path, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite, FileShare.ReadWrite);
            return readOnly ?  fs.CanRead : fs.CanWrite;
        }
        catch
        {
            return false;
        }
    }

    public void AddFileToWhitelist(string path, bool readOnly = true)
    {
        Guard.IsNotNullOrWhiteSpace(path,  nameof(path));
        using var lck = _higherOperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        try
        {
            path = GetFullPath(path);
            _fileListRead.AddOrUpdate(path, s => 0, (s, b) => 0);
            if (!readOnly)
            {
                _fileListWrite.AddOrUpdate(path, s => 0, (s, b) => 0);
            }
        }
        catch
        {
            return;
        }
    }

    public void AddFilesToWhitelist(ImmutableArray<string> paths, bool readOnly = true)
    {
        if (paths.IsDefaultOrEmpty)
            ThrowHelper.ThrowArgumentNullException(nameof(paths));
        foreach (var path in paths)
        {
            AddFileToWhitelist(path, readOnly);
        }
    }


    public void RemoveFileFromAllWhitelists(string path)
    {
        Guard.IsNotNullOrWhiteSpace(path,  nameof(path));
        using var lck = _higherOperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        try
        {
            path = GetFullPath(path);
            _fileListRead.TryRemove(path, out _);
            _fileListWrite.TryRemove(path, out _);
        }
        catch
        {
            return;
        }
    }

    public FluentResults.Result SetReadOnlyWhitelist(ImmutableArray<string> filePaths)
    {
        using var lck = _higherOperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        if (filePaths.IsDefaultOrEmpty)
        {
            return FluentResults.Result.Fail($"{nameof(SetReadOnlyWhitelist)}: FilePaths cannot be empty.");
        }
        
        _fileListRead.Clear();
        var res = new FluentResults.Result();
        foreach (var path in filePaths)
        {
            Guard.IsNotNullOrWhiteSpace(path, nameof(path));
            try
            {
                var p = Path.GetFullPath(path.CleanUpPathCrossPlatform());
                if (_fileListRead.ContainsKey(p))
                {
                    res = res.WithReason(new Success($"Path already in whitelist: {p}"));
                    continue;
                }

                if (_fileListRead.TryAdd(p, 0))
                {
                    res = res.WithSuccess($"Added path successfully: {p}");
                    continue;
                }

                res = res.WithError(new Error($"Failed to add path to list: {p}"));
            }
            catch (Exception e)
            {
                res = res.WithError(new ExceptionalError(e)
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.ExceptionDetails, e.Message)
                    .WithMetadata(MetadataType.RootObject, path)
                );
                continue;
            }
        }

        return res;
    }

    public FluentResults.Result SetReadWriteWhitelist(ImmutableArray<string> filePaths)
    {
        if (filePaths.IsDefaultOrEmpty)
        {
            return FluentResults.Result.Fail($"{nameof(SetReadOnlyWhitelist)}: FilePaths cannot be empty.");
        }
        using var lck = _higherOperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        _fileListRead.Clear();
        _fileListWrite.Clear();
        var res = new FluentResults.Result();
        foreach (var path in filePaths)
        {
            Guard.IsNotNullOrWhiteSpace(path, nameof(path));
            try
            {
                var p = Path.GetFullPath(path.CleanUpPathCrossPlatform());
                TryAddToList(_fileListRead, p);
                TryAddToList(_fileListWrite, p);
                res = res.WithError(new Error($"Failed to add path to list: {p}"));
            }
            catch (Exception e)
            {
                res = res.WithError(new ExceptionalError(e)
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.ExceptionDetails, e.Message)
                    .WithMetadata(MetadataType.RootObject, path)
                );
                continue;
            }
        }

        void TryAddToList(ConcurrentDictionary<string, byte> dict, string p)
        {
            if (dict.ContainsKey(p))
            {
                res = res.WithReason(new Success($"Path already in whitelist: {p}"));
                return;
            }

            if (dict.TryAdd(p, 0))
            {
                res = res.WithSuccess($"Added path successfully: {p}");
                return;
            }
        }

        return res;
    }

    public void ClearAllWhitelists()
    {
        using var lck = _higherOperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        _fileListRead.Clear();
        _fileListWrite.Clear();
    }
}
