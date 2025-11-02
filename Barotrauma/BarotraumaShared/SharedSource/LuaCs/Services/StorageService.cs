using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.Steam;
using FluentResults;
using FluentResults.LuaCs;
using Error = FluentResults.Error;
using Path = Barotrauma.IO.Path;

namespace Barotrauma.LuaCs.Services;

public class StorageService : IStorageService
{
    
    public StorageService(IStorageServiceConfig configData)
    {
        _configData = configData;
        IsSafeModeIO = configData.GlobalSafeIOEnabled;
    }

    private readonly ConcurrentDictionary<string, OneOf.OneOf<byte[], string, XDocument>> _fsCache = new();
    private readonly IStorageServiceConfig _configData;

    public bool IsDisposed => ModUtils.Threading.GetBool(ref _isDisposed);
    private int _isDisposed = 0;
    public void Dispose()
    {
        ModUtils.Threading.SetBool(ref _isDisposed, true);
    }

    public void PurgeCache()
    {
        ((IService)this).CheckDisposed();
        _fsCache.Clear();
    }

    public void PurgeFileFromCache(string absolutePath)
    {
        ((IService)this).CheckDisposed();

        if (absolutePath.IsNullOrWhiteSpace())
            return;

        try
        {
            //sanitation pass
            absolutePath = System.IO.Path.GetFullPath(absolutePath).CleanUpPath();
            _fsCache.Remove(absolutePath, out _);
        }
        catch
        {
            // ignored
            return;
        }
    }

    public void PurgeFilesFromCache(params string[] absolutePaths)
    {
        ((IService)this).CheckDisposed();

        if (absolutePaths.Length < 1)
            return;

        foreach (var path in absolutePaths)
        {
            try
            {
                if (path.IsNullOrWhiteSpace())
                    continue;
                
                //sanitation pass
                var path2 = System.IO.Path.GetFullPath(path).CleanUpPath();
                _fsCache.Remove(path2, out _);
            }
            catch
            {
                // ignored
                continue;
            }
        }
    }

    private int _useCaching;
    public bool UseCaching
    {
        get => ModUtils.Threading.GetBool(ref _useCaching);
        set => ModUtils.Threading.SetBool(ref _useCaching, value);
    }

    private int _isSafeModeIO;

    public bool IsSafeModeIO
    {
        get => ModUtils.Threading.GetBool(ref _isSafeModeIO);
        private set => ModUtils.Threading.SetBool(ref _isSafeModeIO, value);
    }

    public void EnableSafeModeIO()
    {
        ((IService)this).CheckDisposed();
        IsSafeModeIO = true;
    }

    public bool IsFileAccessible(string path, bool readOnly, bool checkSafeOnly = false)
    {
        ((IService)this).CheckDisposed();
        
        if (path.IsNullOrWhiteSpace())
            return false;
        
        try
        {
            path = GetFullPath(path);

            bool pathIsSafe = false;
            if (IsSafeModeIO)
            {
                var dirs = readOnly ? _configData.SafeIOReadDirectories : _configData.SafeIOWriteDirectories;

                if (dirs.Count == 0)
                    return false;

                foreach (var dir in dirs)
                {
                    if (PathStartsWith(path, dir))
                    {
                        pathIsSafe = true;
                        break;
                    }
                }

                if (checkSafeOnly || !pathIsSafe)
                    return pathIsSafe;
            }
            
            using FileStream fs = new FileStream(path, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite, FileShare.Read);
            return readOnly ? fs.CanRead : fs.CanWrite;
        }
        catch
        {
            return false;
        }
        
        bool PathStartsWith(string path, string prefix) => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        string GetFullPath(string path) => System.IO.Path.GetFullPath(path).CleanUpPath();
    }

    public FluentResults.Result<XDocument> LoadLocalXml(ContentPackage package, string localFilePath) =>
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? TryLoadXml(r.Value) : r.ToResult();
    public FluentResults.Result<byte[]> LoadLocalBinary(ContentPackage package, string localFilePath) => 
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? TryLoadBinary(r.Value) : r.ToResult();
    public FluentResults.Result<string> LoadLocalText(ContentPackage package, string localFilePath) => 
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? TryLoadText(r.Value) : r.ToResult();
    public FluentResults.Result SaveLocalXml(ContentPackage package, string localFilePath, XDocument document) =>
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null }
            ? TrySaveXml(r.Value, document) : r.ToResult();
    public FluentResults.Result SaveLocalBinary(ContentPackage package, string localFilePath, in byte[] bytes) => 
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null }
            ? TrySaveBinary(r.Value, bytes) : r.ToResult();
    public FluentResults.Result SaveLocalText(ContentPackage package, string localFilePath, in string text) => 
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null }
            ? TrySaveText(r.Value, text) : r.ToResult();
    public async Task<FluentResults.Result<XDocument>> LoadLocalXmlAsync(ContentPackage package, string localFilePath) =>
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? await TryLoadXmlAsync(r.Value) : r.ToResult();
    public async Task<FluentResults.Result<byte[]>> LoadLocalBinaryAsync(ContentPackage package, string localFilePath) => 
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? await TryLoadBinaryAsync(r.Value) : r.ToResult();
    public async Task<FluentResults.Result<string>> LoadLocalTextAsync(ContentPackage package, string localFilePath) => 
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? await TryLoadTextAsync(r.Value) : r.ToResult();
    public async Task<FluentResults.Result> SaveLocalXmlAsync(ContentPackage package, string localFilePath, XDocument document) => 
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null }
            ? await TrySaveXmlAsync(r.Value, document) : r.ToResult();
    public async Task<FluentResults.Result> SaveLocalBinaryAsync(ContentPackage package, string localFilePath, byte[] bytes) => 
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null }
            ? await TrySaveBinaryAsync(r.Value, bytes) : r.ToResult();
    public async Task<FluentResults.Result> SaveLocalTextAsync(ContentPackage package, string localFilePath, string text) =>
        GetAbsFromLocal(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null }
            ? await TrySaveTextAsync(r.Value, text) : r.ToResult();
    public FluentResults.Result<XDocument> LoadPackageXml(ContentPackage package, string localFilePath) =>
        GetAbsFromPackage(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? TryLoadXml(r.Value) : r.ToResult();
    public FluentResults.Result<byte[]> LoadPackageBinary(ContentPackage package, string localFilePath) => 
        GetAbsFromPackage(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? TryLoadBinary(r.Value) : r.ToResult();
    public FluentResults.Result<string> LoadPackageText(ContentPackage package, string localFilePath) => 
        GetAbsFromPackage(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? TryLoadText(r.Value) : r.ToResult();
    
    
    
    public ImmutableArray<(string, FluentResults.Result<XDocument>)> LoadPackageXmlFiles(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        if (localFilePaths.IsDefaultOrEmpty)
            return ImmutableArray<(string, FluentResults.Result<XDocument>)>.Empty;
        var builder = ImmutableArray.CreateBuilder<(string, FluentResults.Result<XDocument>)>(localFilePaths.Length);
        foreach (var path in localFilePaths)
            builder.Add((path, LoadPackageXml(package, path)));
        return builder.MoveToImmutable();
    }

    public ImmutableArray<(string, FluentResults.Result<byte[]>)> LoadPackageBinaryFiles(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        if (localFilePaths.IsDefaultOrEmpty)
            return ImmutableArray<(string, FluentResults.Result<byte[]>)>.Empty;
        var builder = ImmutableArray.CreateBuilder<(string, FluentResults.Result<byte[]>)>(localFilePaths.Length);
        foreach (var path in localFilePaths)
            builder.Add((path, LoadPackageBinary(package, path)));
        return builder.MoveToImmutable();
    }

    public ImmutableArray<(string, FluentResults.Result<string>)> LoadPackageTextFiles(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        if (localFilePaths.IsDefaultOrEmpty)
            return ImmutableArray<(string, FluentResults.Result<string>)>.Empty;
        var builder = ImmutableArray.CreateBuilder<(string, FluentResults.Result<string>)>(localFilePaths.Length);
        foreach (var path in localFilePaths)
            builder.Add((path, LoadPackageText(package, path)));
        return builder.MoveToImmutable();
    }

    public FluentResults.Result<ImmutableArray<string>> FindFilesInPackage(ContentPackage package, string localSubfolder, string regexFilter, bool searchRecursively)
    {
        ((IService)this).CheckDisposed();
        var r = GetAbsFromPackage(package, localSubfolder);
        if (r is { IsFailed: true })
            return r.ToResult();
        var builder = ImmutableArray.CreateBuilder<(string, FluentResults.Result<ImmutableArray<string>>)>();
        var sOption = searchRecursively ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] arr = Directory.GetFiles(localSubfolder, regexFilter.IsNullOrWhiteSpace() ? "*.*" : regexFilter, sOption);
        return new FluentResults.Result<ImmutableArray<string>>().WithSuccess($"Files found.")
            .WithValue(arr.ToImmutableArray());
    }

    public async Task<FluentResults.Result<XDocument>> LoadPackageXmlAsync(ContentPackage package, string localFilePath) =>
        GetAbsFromPackage(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? await TryLoadXmlAsync(r.Value) : r.ToResult();

    public async Task<FluentResults.Result<byte[]>> LoadPackageBinaryAsync(ContentPackage package, string localFilePath) =>
        GetAbsFromPackage(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? await TryLoadBinaryAsync(r.Value) : r.ToResult();

    public async Task<FluentResults.Result<string>> LoadPackageTextAsync(ContentPackage package, string localFilePath) =>
        GetAbsFromPackage(package, localFilePath) is var r && r is { IsSuccess: true, Value: not null } 
            ? await TryLoadTextAsync(r.Value) : r.ToResult();

    public async Task<ImmutableArray<(string, FluentResults.Result<XDocument>)>> LoadPackageXmlFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        if (localFilePaths.IsDefaultOrEmpty)
            return ImmutableArray<(string, FluentResults.Result<XDocument>)>.Empty;
        var builder = ImmutableArray.CreateBuilder<(string, FluentResults.Result<XDocument>)>(localFilePaths.Length);
        
        await localFilePaths.ParallelForEachAsync(async path =>
        {
            builder.Add((path, await LoadPackageXmlAsync(package, path)));
        }, maxDegreeOfParallelism: 2);
        return builder.MoveToImmutable();
    }

    public async Task<ImmutableArray<(string, FluentResults.Result<byte[]>)>> LoadPackageBinaryFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        if (localFilePaths.IsDefaultOrEmpty)
            return ImmutableArray<(string, FluentResults.Result<byte[]>)>.Empty;
        var builder = ImmutableArray.CreateBuilder<(string, FluentResults.Result<byte[]>)>(localFilePaths.Length);
        await localFilePaths.ParallelForEachAsync(async path =>
        {
            builder.Add((path, await LoadPackageBinaryAsync(package, path)));
        }, maxDegreeOfParallelism: 2);
        return builder.MoveToImmutable();
    }

    public async Task<ImmutableArray<(string, FluentResults.Result<string>)>> LoadPackageTextFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        if (localFilePaths.IsDefaultOrEmpty)
            return ImmutableArray<(string, FluentResults.Result<string>)>.Empty;
        var builder = ImmutableArray.CreateBuilder<(string, FluentResults.Result<string>)>(localFilePaths.Length);
        await localFilePaths.ParallelForEachAsync(async path =>
        {
            builder.Add((path, await LoadPackageTextAsync(package, path)));
        }, maxDegreeOfParallelism: 2);
        return builder.MoveToImmutable();
    }


    public FluentResults.Result<XDocument> TryLoadXml(string filePath, Encoding encoding = null)
    {
        ((IService)this).CheckDisposed();
        var r = TryLoadText(filePath, encoding);
        if (r is { IsSuccess: true, Value: not null })
            return XDocument.Parse(r.Value);
        else
        {
            return r.ToResult<XDocument>(s => null)
                .WithError(GetGeneralError(nameof(LoadLocalXml), filePath));
        }
    }

    public FluentResults.Result<string> TryLoadText(string filePath, Encoding encoding = null)
    {
        ((IService)this).CheckDisposed();
        if (IsSafeModeIO && !IsFileAccessible(filePath, true, true))
            return FluentResults.Result.Fail<string>($"StorageService: Cannot access file '{filePath}'. SafeMode enabled.");
        if (UseCaching && _fsCache.TryGetValue(filePath, out var result) 
                       && result.TryPickT1(out var cachedVal, out _))
        {
            return FluentResults.Result.Ok(cachedVal);
        }
        
        return IOExceptionsOperationRunner(nameof(TryLoadText), filePath, () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            var fileText = encoding is null ? System.IO.File.ReadAllText(fp) : System.IO.File.ReadAllText(fp, encoding);
            if (UseCaching)
                _fsCache[filePath] = fileText;
            return new FluentResults.Result<string>().WithSuccess($"Loaded file successfully").WithValue(fileText);
        });
    }

    public FluentResults.Result<byte[]> TryLoadBinary(string filePath)
    {
        ((IService)this).CheckDisposed();
        if (IsSafeModeIO && !IsFileAccessible(filePath, true, true))
            return FluentResults.Result.Fail<byte[]>($"StorageService: Cannot access file '{filePath}'. SafeMode enabled.");
        if (UseCaching && _fsCache.TryGetValue(filePath, out var result) 
                       && result.TryPickT0(out var cachedVal, out _))
        {
            return FluentResults.Result.Ok(cachedVal);
        }
        
        return IOExceptionsOperationRunner(nameof(TryLoadBinary), filePath, () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            var fileData = System.IO.File.ReadAllBytes(fp);
            if (UseCaching)
                _fsCache[filePath] = fileData;
            return new FluentResults.Result<byte[]>().WithSuccess($"Loaded file successfully").WithValue(fileData);
        });
    }

    public FluentResults.Result TrySaveXml(string filePath, in XDocument document, Encoding encoding = null) => TrySaveText(filePath, document.ToString(), encoding);
    public FluentResults.Result TrySaveText(string filePath, in string text, Encoding encoding = null)
    {
        ((IService)this).CheckDisposed();
        if (text.IsNullOrWhiteSpace())
        {
            return FluentResults.Result.Fail($"Contents are empty for {filePath}")
                .WithError(new Error($"Contents are empty for {filePath}")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.Sources, filePath));
        }
        if (IsSafeModeIO && !IsFileAccessible(filePath, false, true))
            return FluentResults.Result.Fail($"StorageService: Cannot write to file '{filePath}'. SafeMode enabled.");
        string t = text; //copy
        return IOExceptionsOperationRunner(nameof(TrySaveText), filePath, () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            System.IO.File.WriteAllText(fp, t, encoding);
            if (UseCaching)
                _fsCache[filePath] = t;
            return new FluentResults.Result().WithSuccess($"Saved to file successfully");
        });
    }

    public FluentResults.Result TrySaveBinary(string filePath, in byte[] bytes)
    {
        ((IService)this).CheckDisposed();
        if (bytes is null || bytes.Length == 0)
        {
            return FluentResults.Result.Fail($"Byte array is null or empty for {filePath}")
                .WithError(new Error($"Byte array is null or empty for {filePath}")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.Sources, filePath));
        }
        if (IsSafeModeIO && !IsFileAccessible(filePath, false, true))
            return FluentResults.Result.Fail($"StorageService: Cannot write to file '{filePath}'. SafeMode enabled.");
        byte[] b = new byte[bytes.Length];
        System.Buffer.BlockCopy(bytes, 0, b, 0, bytes.Length);
        return IOExceptionsOperationRunner(nameof(TrySaveBinary), filePath, () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            System.IO.File.WriteAllBytes(fp, b);
            if (UseCaching)
                _fsCache[filePath] = b;
            return new FluentResults.Result().WithSuccess($"Saved to file successfully");
        });
    }

    public FluentResults.Result<bool> FileExists(string filePath)
    {
        ((IService)this).CheckDisposed();
        return IOExceptionsOperationRunner<bool>(nameof(FileExists), filePath, () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            return System.IO.File.Exists(fp);
        });
    }

    public FluentResults.Result<bool> DirectoryExists(string directoryPath)
    {
        ((IService)this).CheckDisposed();
        try
        {
            var di = new DirectoryInfo(directoryPath);
            return di.Exists;
        }
        catch (Exception ex)
        {
            return new FluentResults.Result<bool>().WithError(ex.Message);
        }
    }

    public async Task<FluentResults.Result<XDocument>> TryLoadXmlAsync(string filePath, Encoding encoding = null)
    {
        ((IService)this).CheckDisposed();
        if (IsSafeModeIO && !IsFileAccessible(filePath, true, true))
            return FluentResults.Result.Fail<XDocument>($"StorageService: Cannot access file '{filePath}'. SafeMode enabled.");
        if (UseCaching && _fsCache.TryGetValue(filePath, out var cachedVal) 
                       && cachedVal.TryPickT2(out var cachedDoc, out _))
            return FluentResults.Result.Ok(cachedDoc);
        try
        {
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var doc = await XDocument.LoadAsync(fs, LoadOptions.PreserveWhitespace, CancellationToken.None);
            if (UseCaching)
                _fsCache[filePath] = doc;
            return FluentResults.Result.Ok(doc);
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail<XDocument>(GetGeneralError(nameof(TryLoadXmlAsync), filePath));
        }
    }

    public async Task<FluentResults.Result<string>> TryLoadTextAsync(string filePath, Encoding encoding = null)
    {
        ((IService)this).CheckDisposed();
        if (IsSafeModeIO && !IsFileAccessible(filePath, true, true))
            return FluentResults.Result.Fail<string>($"StorageService: Cannot access file '{filePath}'. SafeMode enabled.");
        if (UseCaching && _fsCache.TryGetValue(filePath, out var cachedVal) 
                       && cachedVal.TryPickT1(out var cachedTxt, out _))
            return FluentResults.Result.Ok(cachedTxt);
            
        return await IOExceptionsOperationRunnerAsync<string>(nameof(TryLoadTextAsync), filePath, async () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            var txt = await System.IO.File.ReadAllTextAsync(fp);
            if (UseCaching)
                _fsCache[filePath] = txt;
            return FluentResults.Result.Ok(txt);
        });
    }

    public async Task<FluentResults.Result<byte[]>> TryLoadBinaryAsync(string filePath)
    {
        ((IService)this).CheckDisposed();
        if (IsSafeModeIO && !IsFileAccessible(filePath, true, true))
            return FluentResults.Result.Fail<byte[]>($"StorageService: Cannot access file '{filePath}'. SafeMode enabled.");
        if (UseCaching && _fsCache.TryGetValue(filePath, out var cachedVal)
                       && cachedVal.TryPickT0(out var cachedBin, out _))
        {
            return cachedBin;
        }
        
        return await IOExceptionsOperationRunnerAsync<byte[]>(nameof(TryLoadTextAsync), filePath, async () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            return await System.IO.File.ReadAllBytesAsync(fp);
        });
    }

    public async Task<FluentResults.Result> TrySaveXmlAsync(string filePath, XDocument document, Encoding encoding = null) => await TrySaveTextAsync(filePath, document.ToString(), encoding);
    public async Task<FluentResults.Result> TrySaveTextAsync(string filePath, string text, Encoding encoding = null)
    {
        ((IService)this).CheckDisposed();
        if (IsSafeModeIO && !IsFileAccessible(filePath, false, true))
            return FluentResults.Result.Fail($"StorageService: Cannot access file '{filePath}'. SafeMode enabled.");
        if (text.IsNullOrWhiteSpace())
        {
            return FluentResults.Result.Fail($"Contents are empty for {filePath}")
                .WithError(new Error($"Contents are empty for {filePath}")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.Sources, filePath));
        }

        string t = text.ToString(); //copy
        return await IOExceptionsOperationRunnerAsync(nameof(TrySaveText), filePath, async () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            await System.IO.File.WriteAllTextAsync(fp, t, encoding);
            if (UseCaching)
                _fsCache[filePath] = t;
            return new FluentResults.Result().WithSuccess($"Saved to file successfully");
        });
    }

    public async Task<FluentResults.Result> TrySaveBinaryAsync(string filePath, byte[] bytes)
    {
        ((IService)this).CheckDisposed();
        if (IsSafeModeIO && !IsFileAccessible(filePath, false, true))
            return FluentResults.Result.Fail($"StorageService: Cannot access file '{filePath}'. SafeMode enabled.");
        if (bytes is null || bytes.Length == 0)
        {
            return FluentResults.Result.Fail($"Byte array is null or empty for {filePath}")
                .WithError(new Error($"Byte array is null or empty for {filePath}")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.Sources, filePath));
        }
        byte[] b = new byte[bytes.Length];
        System.Buffer.BlockCopy(bytes, 0, b, 0, bytes.Length);
        return await IOExceptionsOperationRunnerAsync(nameof(TrySaveBinary), filePath, async () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            await System.IO.File.WriteAllBytesAsync(fp, b);
            if (UseCaching)
                _fsCache[filePath] = b;
            return new FluentResults.Result().WithSuccess($"Saved to file successfully");
        });
    }
    
    private async Task<FluentResults.Result<T>> IOExceptionsOperationRunnerAsync<T>(string funcName, string filepath, Func<Task<FluentResults.Result<T>>> operation) 
    {
        try
        {
            return await operation?.Invoke()!;
        }
        catch (ArgumentNullException ane)
        {
            return ReturnException(ane, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (ArgumentException ae)
        {
            return ReturnException(ae, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (PathTooLongException ptle)
        {
            return ReturnException(ptle, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (NotSupportedException nse)
        {
            return ReturnException(nse, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (UnauthorizedAccessException uae)
        {
            return ReturnException(uae, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (DirectoryNotFoundException dnfe)
        {
            return ReturnException(dnfe, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (FileNotFoundException fnfe)
        {
            return ReturnException(fnfe, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (SecurityException se)
        {
            return ReturnException(se, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (IOException ioe)
        {
            return ReturnException(ioe, filepath).WithError(GetGeneralError(nameof(SaveLocalXml), filepath));
        }
    }
    
    private async Task<FluentResults.Result> IOExceptionsOperationRunnerAsync(string funcName, string filepath, Func<Task<FluentResults.Result>> operation) 
    {
        try
        {
            return await operation?.Invoke()!;
        }
        catch (ArgumentNullException ane)
        {
            return ReturnException(ane, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (ArgumentException ae)
        {
            return ReturnException(ae, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (PathTooLongException ptle)
        {
            return ReturnException(ptle, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (NotSupportedException nse)
        {
            return ReturnException(nse, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (UnauthorizedAccessException uae)
        {
            return ReturnException(uae, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (DirectoryNotFoundException dnfe)
        {
            return ReturnException(dnfe, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (FileNotFoundException fnfe)
        {
            return ReturnException(fnfe, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (SecurityException se)
        {
            return ReturnException(se, filepath).WithError(GetGeneralError(funcName, filepath));
        }
        catch (IOException ioe)
        {
            return ReturnException(ioe, filepath).WithError(GetGeneralError(nameof(SaveLocalXml), filepath));
        }
    }
    
    private FluentResults.Result<T> IOExceptionsOperationRunner<T>(string funcName, string filepath, Func<FluentResults.Result<T>> operation)
    {
        try
        {
            return operation?.Invoke();
        }
        catch (ArgumentNullException ane)
        {
            return ReturnException(ane, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (ArgumentException ae)
        {
            return ReturnException(ae, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (PathTooLongException ptle)
        {
            return ReturnException(ptle, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (NotSupportedException nse)
        {
            return ReturnException(nse, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (UnauthorizedAccessException uae)
        {
            return ReturnException(uae, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (DirectoryNotFoundException dnfe)
        {
            return ReturnException(dnfe, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (FileNotFoundException fnfe)
        {
            return ReturnException(fnfe, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (SecurityException se)
        {
            return ReturnException(se, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (IOException ioe)
        {
            return ReturnException(ioe, filepath)
                .WithError(GetGeneralError(nameof(SaveLocalXml), filepath));
        }
    }
    
    private FluentResults.Result IOExceptionsOperationRunner(string funcName, string filepath, Func<FluentResults.Result> operation)
    {
        try
        {
            return operation?.Invoke();
        }
        catch (ArgumentNullException ane)
        {
            return ReturnException(ane, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (ArgumentException ae)
        {
            return ReturnException(ae, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (PathTooLongException ptle)
        {
            return ReturnException(ptle, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (NotSupportedException nse)
        {
            return ReturnException(nse, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (UnauthorizedAccessException uae)
        {
            return ReturnException(uae, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (DirectoryNotFoundException dnfe)
        {
            return ReturnException(dnfe, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (FileNotFoundException fnfe)
        {
            return ReturnException(fnfe, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (SecurityException se)
        {
            return ReturnException(se, filepath)
                .WithError(GetGeneralError(funcName, filepath));
        }
        catch (IOException ioe)
        {
            return ReturnException(ioe, filepath)
                .WithError(GetGeneralError(nameof(SaveLocalXml), filepath));
        }
    }
    
    private Error GetGeneralError(string funcName, string localfp, ContentPackage package) =>
        new Error($"{funcName}: Failed to load local file.")
            .WithMetadata(MetadataType.ExceptionObject, this)
            .WithMetadata(MetadataType.Sources, localfp)
            .WithMetadata(MetadataType.RootObject, package);
    
    private Error GetGeneralError(string funcName, string localfp) =>
        new Error($"{funcName}: Failed to load local file.")
            .WithMetadata(MetadataType.ExceptionObject, this)
            .WithMetadata(MetadataType.Sources, localfp);
    
    private FluentResults.Result<string> GetAbsFromLocal(ContentPackage package, string localFilePath)
    {
        if (Path.IsPathRooted(localFilePath))
        {
            return new FluentResults.Result<string>().WithError(
                new Error($"The path '{localFilePath}' is a rooted path. Must be relative!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, localFilePath));
        }

        if (package is null)
        {
            return new FluentResults.Result<string>().WithError(
                new Error($"{nameof(GetAbsFromPackage)} The package reference for {localFilePath} is null!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, localFilePath));
        }
        
        return new FluentResults.Result<string>().WithSuccess($"Path constructed")
            .WithValue(System.IO.Path.GetFullPath(System.IO.Path.Combine(
            _configData.RunLocation,
            _configData.LocalPackageDataPath.Replace(
                _configData.LocalDataPathRegex, 
                package.TryExtractSteamWorkshopId(out var id) ? id.Value.ToString() : package.Name), 
            localFilePath)));
    }

    public FluentResults.Result<string> GetAbsFromPackage(ContentPackage package, string localFilePath)
    {
        if (package is null)
        {
            return new FluentResults.Result<string>().WithError(
                new Error($"{nameof(GetAbsFromPackage)} The package reference for {localFilePath} is null!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, localFilePath));
        }
        
        if (localFilePath.IsNullOrWhiteSpace())
        {
            return new FluentResults.Result<string>().WithValue(Path.GetFullPath(package.Path.CleanUpPath()));
        }
        
        var path = localFilePath.CleanUpPath();
        
        if (Path.IsPathRooted(path))
        {
            return new FluentResults.Result<string>().WithError(
                new Error($"The path '{localFilePath}' is a rooted path. Must be relative!")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, localFilePath));
        }
        
        return new FluentResults.Result<string>().WithSuccess($"Path constructed")
            .WithValue(Path.Combine(Path.GetFullPath(package.Path.CleanUpPath()), path));
    }
    
    private FluentResults.Result<TReturn> ReturnException<TReturn, TException>(TException exception, ContentPackage package) where TException : Exception
    {
        return new FluentResults.Result<TReturn>().WithError(new ExceptionalError(exception)
            .WithMetadata(MetadataType.ExceptionObject, this)
            .WithMetadata(MetadataType.RootObject, package));
    }
    
    private FluentResults.Result ReturnException<TException>(TException exception, ContentPackage package) where TException : Exception
    {
        return new FluentResults.Result().WithError(new ExceptionalError(exception)
            .WithMetadata(MetadataType.ExceptionObject, this)
            .WithMetadata(MetadataType.RootObject, package));
    }
    
    private FluentResults.Result ReturnException<TException>(TException exception, string filePath) where TException : Exception
    {
        return new FluentResults.Result().WithError(new ExceptionalError(exception)
            .WithMetadata(MetadataType.ExceptionObject, this)
            .WithMetadata(MetadataType.RootObject, filePath));
    }
}
