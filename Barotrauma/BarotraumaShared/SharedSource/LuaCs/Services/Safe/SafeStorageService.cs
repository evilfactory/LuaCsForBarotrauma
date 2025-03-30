using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Barotrauma.IO;
using Barotrauma.LuaCs.Data;
using FarseerPhysics.Common;
using FluentResults;

namespace Barotrauma.LuaCs.Services.Safe;

public class SafeStorageService : StorageService, ISafeStorageService
{
    private ConcurrentDictionary<string, byte> _fileListRead = new (), _fileListReadWrite = new();
    
    public SafeStorageService(IStorageServiceConfig configData) : base(configData)
    {
        
    }
    
    private string GetFullPath(string path) => System.IO.Path.GetFullPath(path).CleanUpPathCrossPlatform();

    public bool IsFileAccessible(string path, bool readOnly, bool checkWhitelistOnly = true)
    {
        ((IService)this).CheckDisposed();
        
        try
        {
            path = GetFullPath(path);
            if (!readOnly && IsReadOnlyMode)
                return false;
            if (readOnly)
            {
                if (!_fileListRead.ContainsKey(path))
                    return false;
            }
            else
            {
                if (!_fileListReadWrite.ContainsKey(path))
                    return false;
            }
            if (checkWhitelistOnly)
                return true;
            
            using var fs = System.IO.File.Open(
                path, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite, FileShare.ReadWrite);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void AddFileToWhitelist(string path, bool readOnly = true)
    {
        ((IService)this).CheckDisposed();
        try
        {
            path = GetFullPath(path);
            _fileListRead.AddOrUpdate(path, s => 0, (s, b) => 0);
            if (!readOnly && !IsReadOnlyMode)
                _fileListRead.AddOrUpdate(path, s => 0, (s, b) => 0);
        }
        catch
        {
            return;
        }
    }

    public void RemoveFileFromAllWhitelists(string path)
    {
        ((IService)this).CheckDisposed();
        try
        {
            path = GetFullPath(path);
            _fileListRead.TryRemove(path, out _);
            _fileListReadWrite.TryRemove(path, out _);
        }
        catch
        {
            return;
        }
    }

    public FluentResults.Result SetReadOnlyWhitelist(ImmutableArray<string> filePaths)
    {
        ((IService)this).CheckDisposed();
        if (filePaths.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(SetReadOnlyWhitelist)}: FilePaths cannot be empty.");
        var res = new FluentResults.Result();
        foreach (var path in filePaths)
        {
            // TODO: Cleanup path and add it.
        }

        throw new NotImplementedException();
    }

    public FluentResults.Result SetReadWriteWhitelist(ImmutableArray<string> filePaths)
    {
        ((IService)this).CheckDisposed();
        throw new System.NotImplementedException();
    }

    public void ClearAllWhitelists()
    {
        throw new System.NotImplementedException();
    }

    private int _isReadOnlyMode = 0;
    public bool IsReadOnlyMode => ModUtils.Threading.GetBool(ref _isReadOnlyMode);
    
    public bool EnableReadOnlyMode()
    {
        ModUtils.Threading.SetBool(ref _isReadOnlyMode, true);
        return ModUtils.Threading.GetBool(ref _isReadOnlyMode);
    }
    
    
}
