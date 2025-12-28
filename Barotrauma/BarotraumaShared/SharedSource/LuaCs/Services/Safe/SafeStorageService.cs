using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.IO;
using Barotrauma.LuaCs.Data;
using FarseerPhysics.Common;
using FluentResults;
using FluentResults.LuaCs;
using Path = System.IO.Path;

namespace Barotrauma.LuaCs.Services.Safe;

public class SafeStorageService : StorageService, ISafeStorageService
{
    private ConcurrentDictionary<string, byte> _fileListRead = new (), _fileListWrite = new();
    
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
            if (!_fileListRead.ContainsKey(path))
                return false;
            if (!readOnly && !_fileListWrite.ContainsKey(path))
                return false;
            if (checkWhitelistOnly)
                return true;
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
        ((IService)this).CheckDisposed();
        try
        {
            path = GetFullPath(path);
            _fileListRead.AddOrUpdate(path, s => 0, (s, b) => 0);
            if (!readOnly)
                _fileListWrite.AddOrUpdate(path, s => 0, (s, b) => 0);
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
            _fileListWrite.TryRemove(path, out _);
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
        _fileListRead.Clear();
        var res = new FluentResults.Result();
        foreach (var path in filePaths)
        {
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
        ((IService)this).CheckDisposed();
        if (filePaths.IsDefaultOrEmpty)
            return FluentResults.Result.Fail($"{nameof(SetReadOnlyWhitelist)}: FilePaths cannot be empty.");
        _fileListRead.Clear();
        _fileListWrite.Clear();
        var res = new FluentResults.Result();
        foreach (var path in filePaths)
        {
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
        ((IService)this).CheckDisposed();
        _fileListRead.Clear();
        _fileListWrite.Clear();
    }

    #region Base_Overrides

    private bool ReadCheck(string path)
    {
        return IsFileAccessible(path, true, true);
    }

    private bool WriteCheck(string path)
    {
        return IsFileAccessible(path, false, true);
    }
    
    public override Result<bool> FileExists(string filePath)
    {
        if (!ReadCheck(filePath))
            return FluentResults.Result.Fail("Cannot access file.");
        return base.FileExists(filePath);
    }

    public override Result<byte[]> TryLoadBinary(string filePath)
    {
        if (!ReadCheck(filePath))
            return FluentResults.Result.Fail("Cannot access file.");
        return base.TryLoadBinary(filePath);
    }

    public override async Task<Result<byte[]>> TryLoadBinaryAsync(string filePath)
    {
        if (!ReadCheck(filePath))
            return FluentResults.Result.Fail("Cannot access file.");
        return await base.TryLoadBinaryAsync(filePath);
    }

    public override Result<string> TryLoadText(string filePath, Encoding encoding = null)
    {
        if (!ReadCheck(filePath))
            return FluentResults.Result.Fail("Cannot access file.");
        return base.TryLoadText(filePath, encoding);
    }

    public override async Task<Result<string>> TryLoadTextAsync(string filePath, Encoding encoding = null)
    {
        if (!ReadCheck(filePath))
            return FluentResults.Result.Fail("Cannot access file.");
        return await base.TryLoadTextAsync(filePath, encoding);
    }

    public override Result<XDocument> TryLoadXml(string filePath, Encoding encoding = null)
    {
        if (!ReadCheck(filePath))
            return FluentResults.Result.Fail("Cannot access file.");
        return base.TryLoadXml(filePath, encoding);
    }

    public override async Task<Result<XDocument>> TryLoadXmlAsync(string filePath, Encoding encoding = null)
    {
        if (!ReadCheck(filePath))
            return FluentResults.Result.Fail("Cannot access file.");
        return await base.TryLoadXmlAsync(filePath, encoding);
    }

    public override FluentResults.Result TrySaveBinary(string filePath, in byte[] bytes)
    {
        if (!WriteCheck(filePath))
            return FluentResults.Result.Fail("Cannot write to file.");
        return base.TrySaveBinary(filePath, in bytes);
    }

    public override async Task<FluentResults.Result> TrySaveBinaryAsync(string filePath, byte[] bytes)
    {
        if (!WriteCheck(filePath))
            return FluentResults.Result.Fail("Cannot write to file.");
        return await base.TrySaveBinaryAsync(filePath, bytes);
    }

    public override FluentResults.Result TrySaveText(string filePath, in string text, Encoding encoding = null)
    {
        if (!WriteCheck(filePath))
            return FluentResults.Result.Fail("Cannot write to file.");
        return base.TrySaveText(filePath, in text, encoding);
    }

    public override async Task<FluentResults.Result> TrySaveTextAsync(string filePath, string text, Encoding encoding = null)
    {
        if (!WriteCheck(filePath))
            return FluentResults.Result.Fail("Cannot write to file.");
        return await base.TrySaveTextAsync(filePath, text, encoding);
    }

    public override FluentResults.Result TrySaveXml(string filePath, in XDocument document, Encoding encoding = null)
    {
        if (!WriteCheck(filePath))
            return FluentResults.Result.Fail("Cannot write to file.");
        return base.TrySaveXml(filePath, in document, encoding);
    }

    public override async Task<FluentResults.Result> TrySaveXmlAsync(string filePath, XDocument document, Encoding encoding = null)
    {
        if (!WriteCheck(filePath))
            return FluentResults.Result.Fail("Cannot write to file.");
        return await base.TrySaveXmlAsync(filePath, document, encoding);
    }
    
    #endregion
    
    
}
