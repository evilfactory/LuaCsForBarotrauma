using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.Networking;
using FluentResults;
using FluentResults.LuaCs;
using Microsoft.Toolkit.Diagnostics;
using Error = FluentResults.Error;
using Path = System.IO.Path;

namespace Barotrauma.LuaCs.Services;

public class StorageService : IStorageService
{
    public StorageService(IStorageServiceConfig configData)
    {
        ConfigData = configData;
        IsReadOperationAllowedEval = async Task<bool> (str) => true;
        IsWriteOperationAllowedEval = async Task<bool> (str) => true;
    }

    public StorageService(IStorageServiceConfig configData, 
        Func<string, Task<bool>> isReadOperationAllowedEval, 
        Func<string, Task<bool>> isWriteOperationAllowedEval)
    {
        Guard.IsNotNull(isReadOperationAllowedEval, nameof(isReadOperationAllowedEval));
        Guard.IsNotNull(isWriteOperationAllowedEval, nameof(isWriteOperationAllowedEval));
        ConfigData = configData;
        IsReadOperationAllowedEval = isReadOperationAllowedEval;
        IsWriteOperationAllowedEval = isWriteOperationAllowedEval;
    }

    private readonly ConcurrentDictionary<string, OneOf.OneOf<byte[], string, XDocument>> _fsCache = new();
    protected readonly IStorageServiceConfig ConfigData;
    protected readonly AsyncReaderWriterLock OperationsLock = new();

    private Func<string, Task<bool>> _isReadOperationAllowedEval;
    protected Func<string, Task<bool>> IsReadOperationAllowedEval
    {
        get => _isReadOperationAllowedEval;
        set
        {
            if (value is not null)
                _isReadOperationAllowedEval = value;
        }
    }

    private Func<string, Task<bool>> _isWriteOperationAllowedEval;
    protected Func<string, Task<bool>> IsWriteOperationAllowedEval
    {
        get => _isWriteOperationAllowedEval;
        set
        {
            if (value is not null)
                _isWriteOperationAllowedEval = value;
        }
    }
    
    public bool IsDisposed => ModUtils.Threading.GetBool(ref _isDisposed);
    private int _isDisposed = 0;
    public virtual void Dispose()
    {
        using var lck = OperationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
            return;
        _fsCache.Clear();
    }

    public void PurgeCache()
    {
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        _fsCache.Clear();
    }

    public void PurgeFileFromCache(string absolutePath)
    {
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

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
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

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

    // --- Local Game Content
    protected Result<string> GetAbsolutePathForLocal(ContentPackage package, string localFilePath)
    {
        if (Path.IsPathRooted(localFilePath))
            ThrowHelper.ThrowArgumentException($"{nameof(GetAbsolutePathForLocal)}: The path {localFilePath} is an absolute path.");

        try
        {
            var path = System.IO.Path.GetFullPath(Path.Combine(
                ConfigData.LocalPackageDataPath.Replace(ConfigData.LocalDataPathRegex, package.ToIdentifier().Value)
                    .CleanUpPathCrossPlatform(),
                localFilePath));
            if (!path.StartsWith(ConfigData.LocalDataSavePath))
                ThrowHelper.ThrowUnauthorizedAccessException($"{nameof(GetAbsolutePathForLocal)}: The local path of '{path}' is not a local path!");
            return path;
        }
        catch (Exception e)
        {
            if (e is ArgumentNullException or ArgumentException or UnauthorizedAccessException)
                throw;    // these are dev errors and should be propagated.
            return FluentResults.Result.Fail(new ExceptionalError(e));
        }
    }

    private Result<T> LoadLocalData<T>(ContentPackage package, string localFilePath, Func<string, Result<T>> dataLoader)
    {
        Guard.IsNotNull(package, nameof(package));
        Guard.IsNotNullOrWhiteSpace(localFilePath, nameof(localFilePath));
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        var res = GetAbsolutePathForLocal(package, localFilePath);
        return res is { IsFailed: true } ? res.ToResult() : dataLoader(res.Value);
    }
    
    public Result<XDocument> LoadLocalXml(ContentPackage package, string localFilePath) => LoadLocalData(package, localFilePath, TryLoadXml);
    public Result<byte[]> LoadLocalBinary(ContentPackage package, string localFilePath) => LoadLocalData(package, localFilePath, TryLoadBinary);
    public Result<string> LoadLocalText(ContentPackage package, string localFilePath) => LoadLocalData(package, localFilePath, TryLoadText);
    
    
    private FluentResults.Result SaveLocalData<T>(ContentPackage package, string localFilePath, in T data, Func<string, T, FluentResults.Result> dataSaver)
    {
        Guard.IsNotNull(package, nameof(package));
        Guard.IsNotNullOrWhiteSpace(localFilePath, nameof(localFilePath));
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        var res = GetAbsolutePathForLocal(package, localFilePath);
        return res is { IsFailed: true } ? res.ToResult() : dataSaver(res.Value, data);
    }

    public FluentResults.Result SaveLocalXml(ContentPackage package, string localFilePath, XDocument document) 
        => SaveLocalData(package, localFilePath, document, (path, data) => TrySaveXml(path, in data));
    public FluentResults.Result SaveLocalBinary(ContentPackage package, string localFilePath, in byte[] bytes)
        => SaveLocalData(package, localFilePath, bytes, (path, data) => TrySaveBinary(path, in data));
    public FluentResults.Result SaveLocalText(ContentPackage package, string localFilePath, in string text)
        => SaveLocalData(package, localFilePath, text, (path, data) => TrySaveText(path, in data));

    private async Task<Result<T>> LoadLocalDataAsync<T>(ContentPackage package, string localFilePath,
        Func<string, Task<Result<T>>> dataLoader)
    {
        Guard.IsNotNull(package, nameof(package));
        Guard.IsNotNullOrWhiteSpace(localFilePath, nameof(localFilePath));
        using var lck = await OperationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        var res = GetAbsolutePathForLocal(package, localFilePath);
        return res is { IsFailed: true } ? res.ToResult() : await dataLoader(res.Value);
    }

    public async Task<Result<XDocument>> LoadLocalXmlAsync(ContentPackage package, string localFilePath)
        => await LoadLocalDataAsync(package, localFilePath, async path => await TryLoadXmlAsync(path));
    public async Task<Result<byte[]>> LoadLocalBinaryAsync(ContentPackage package, string localFilePath)
        => await LoadLocalDataAsync(package, localFilePath, async path => await TryLoadBinaryAsync(path));
    public async Task<Result<string>> LoadLocalTextAsync(ContentPackage package, string localFilePath) 
        => await LoadLocalDataAsync(package, localFilePath, async path => await TryLoadTextAsync(path));

    private async Task<FluentResults.Result> SaveLocalDataAsync<T>(ContentPackage package, string localFilePath,
        T data, Func<string, T, Task<FluentResults.Result>> dataSaver)
    {
        Guard.IsNotNull(package, nameof(package));
        Guard.IsNotNullOrWhiteSpace(localFilePath, nameof(localFilePath));
        IService.CheckDisposed(this);
        using var lck = await OperationsLock.AcquireReaderLock();
        var res = GetAbsolutePathForLocal(package, localFilePath);
        return res is { IsFailed: true } ? res.ToResult() : await dataSaver(res.Value, data);
    }

    public async Task<FluentResults.Result> SaveLocalXmlAsync(ContentPackage package, string localFilePath, XDocument document) 
        => await SaveLocalDataAsync(package, localFilePath, document, async (path, doc) => await TrySaveXmlAsync(path, doc));
    public async Task<FluentResults.Result> SaveLocalBinaryAsync(ContentPackage package, string localFilePath, byte[] bytes)
        => await SaveLocalDataAsync(package, localFilePath, bytes, async (path, bin) => await TrySaveBinaryAsync(path, bin));
    public async Task<FluentResults.Result> SaveLocalTextAsync(ContentPackage package, string localFilePath, string text)
        => await SaveLocalDataAsync(package, localFilePath, text, async (path, txt) => await TrySaveTextAsync(path, txt));

    
    // --- Package Content
    private Result<T> LoadPackageData<T>(ContentPath filePath, Func<string, Result<T>> dataLoader)
    {
        Guard.IsNotNull(filePath, nameof(filePath));
        Guard.IsNotNullOrWhiteSpace(filePath.FullPath,  nameof(filePath.FullPath));
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        if (!filePath.FullPath.StartsWith(ConfigData.WorkshopModsDirectory) 
            && !filePath.FullPath.StartsWith(ConfigData.LocalModsDirectory)
            && !filePath.FullPath.StartsWith(ContentPackageManager.VanillaCorePackage!.Dir)
#if CLIENT
            && !filePath.FullPath.StartsWith(ConfigData.TempDownloadsDirectory)
#endif
            )
        {
            ThrowHelper.ThrowUnauthorizedAccessException($"{nameof(LoadPackageData)}: The filepath of `{filePath.FullPath}' is not in a package directory!");
        }
        return dataLoader(filePath.FullPath);
    }

    public Result<XDocument> LoadPackageXml(ContentPath filePath)
        => LoadPackageData(filePath, path => TryLoadXml(filePath.FullPath));
    public Result<byte[]> LoadPackageBinary(ContentPath filePath)
        => LoadPackageData(filePath, path => TryLoadBinary(filePath.FullPath));
    public Result<string> LoadPackageText(ContentPath filePath)
        => LoadPackageData(filePath, path => TryLoadText(filePath.FullPath));

    private ImmutableArray<(ContentPath, Result<T>)> LoadPackageDataFiles<T>(ImmutableArray<ContentPath> filePaths, Func<string, Result<T>> dataLoader)
    {
        if (filePaths.IsDefaultOrEmpty)
            ThrowHelper.ThrowArgumentNullException($"{nameof(LoadPackageData)}: File paths is empty!");
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        var builder = ImmutableArray.CreateBuilder<(ContentPath, Result<T>)>();
        foreach (var path in filePaths)
        {
            builder.Add((path, LoadPackageData(path, dataLoader)));
        }
        return builder.MoveToImmutable();
    }

    public ImmutableArray<(ContentPath, Result<XDocument>)> LoadPackageXmlFiles(ImmutableArray<ContentPath> filePaths)
        => LoadPackageDataFiles(filePaths, TryLoadXml);
    public ImmutableArray<(ContentPath, Result<byte[]>)> LoadPackageBinaryFiles(ImmutableArray<ContentPath> filePaths)
        => LoadPackageDataFiles(filePaths, TryLoadBinary);
    public ImmutableArray<(ContentPath, Result<string>)> LoadPackageTextFiles(ImmutableArray<ContentPath> filePaths)
        => LoadPackageDataFiles(filePaths, TryLoadText);

    public Result<ImmutableArray<string>> FindFilesInPackage(ContentPackage package, string localSubfolder, string regexFilter, bool searchRecursively)
    {
        Guard.IsNotNull(package, nameof(package));
        try
        {
            var fullPath = localSubfolder.IsNullOrWhiteSpace()
                ? Path.GetFullPath(package.Path)
                : Path.GetFullPath(package.Path, localSubfolder);
            return System.IO.Directory.GetFiles(fullPath, regexFilter, 
                searchRecursively ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .ToImmutableArray();
        }
        catch (Exception e)
        {
            if (e is ArgumentNullException or ArgumentException)
                throw;
            return FluentResults.Result.Fail(new ExceptionalError(e)
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, package));
        }
    }
    
    
    private async Task<Result<T>> LoadPackageDataAsync<T>(ContentPath filePath, Func<string, Task<Result<T>>> dataLoader)
    {
        Guard.IsNotNull(filePath, nameof(filePath));
        Guard.IsNotNullOrWhiteSpace(filePath.FullPath,  nameof(filePath.FullPath));
        using var lck = await OperationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        if (!filePath.FullPath.StartsWith(ConfigData.WorkshopModsDirectory) && !filePath.FullPath.StartsWith(ConfigData.LocalModsDirectory))
        {
            ThrowHelper.ThrowUnauthorizedAccessException(
                $"{nameof(LoadPackageData)}: The filepath of `{filePath.FullPath}' is not in a package directory!");
        }
        return await dataLoader(filePath.FullPath);
    }
    
    public async Task<Result<XDocument>> LoadPackageXmlAsync(ContentPath filePath)
        => await LoadPackageDataAsync(filePath, async path => await TryLoadXmlAsync(path));
    public async Task<Result<byte[]>> LoadPackageBinaryAsync(ContentPath filePath)
        => await LoadPackageDataAsync(filePath, async path => await TryLoadBinaryAsync(path));
    public async Task<Result<string>> LoadPackageTextAsync(ContentPath filePath)
        => await LoadPackageDataAsync(filePath, async path => await TryLoadTextAsync(path));

    private async Task<ImmutableArray<(ContentPath, Result<T>)>> LoadPackageDataFilesAsync<T>(
        ImmutableArray<ContentPath> filePaths, Func<string, Task<Result<T>>> dataLoader)
    {
        if (filePaths.IsDefaultOrEmpty)
        {
            ThrowHelper.ThrowArgumentNullException($"{nameof(LoadPackageData)}: File paths is empty!");
        }
        using var lck = await OperationsLock.AcquireReaderLock();
        var builder = ImmutableArray.CreateBuilder<(ContentPath, Result<T>)>();
        foreach (var path in filePaths)
        {
            builder.Add((path, await LoadPackageDataAsync(path, dataLoader)));
        }
        return builder.MoveToImmutable();
    }
    
    public async Task<ImmutableArray<(ContentPath, Result<XDocument>)>> LoadPackageXmlFilesAsync(ImmutableArray<ContentPath> filePaths)
        => await LoadPackageDataFilesAsync(filePaths, async path => await TryLoadXmlAsync(path));
    public async Task<ImmutableArray<(ContentPath, Result<byte[]>)>> LoadPackageBinaryFilesAsync(ImmutableArray<ContentPath> filePaths)
        => await LoadPackageDataFilesAsync(filePaths, async path => await TryLoadBinaryAsync(path));
    public async Task<ImmutableArray<(ContentPath, Result<string>)>> LoadPackageTextFilesAsync(ImmutableArray<ContentPath> filePaths)
        => await LoadPackageDataFilesAsync(filePaths, async path => await TryLoadTextAsync(path));
    

    private int _useCaching;
    public bool UseCaching
    {
        get => ModUtils.Threading.GetBool(ref _useCaching);
        set => ModUtils.Threading.SetBool(ref _useCaching, value);
    }

    // Method group redirect
    private FluentResults.Result<XDocument> TryLoadXml(string filePath) => TryLoadXml(filePath, null);
    
    public virtual FluentResults.Result<XDocument> TryLoadXml(string filePath, Encoding encoding)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        var r = TryLoadText(filePath, encoding);
        if (r is { IsSuccess: true, Value: not null })
            return XDocument.Parse(r.Value);
        else
        {
            return r.ToResult<XDocument>(s => null)
                .WithError(GetGeneralError(nameof(LoadLocalXml), filePath));
        }
    }

    // Method group redirect
    private FluentResults.Result<string> TryLoadText(string filePath) => TryLoadText(filePath, null);
    public virtual FluentResults.Result<string> TryLoadText(string filePath, Encoding encoding)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        if (IsReadOperationAllowedEval?.Invoke(filePath).ConfigureAwait(false).GetAwaiter().GetResult() is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TryLoadText)}: File '{filePath}' is not allowed.");
        }
            
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

    public virtual FluentResults.Result<byte[]> TryLoadBinary(string filePath)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        if (IsReadOperationAllowedEval?.Invoke(filePath).ConfigureAwait(false).GetAwaiter().GetResult() is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TryLoadBinary)}: File '{filePath}' is not allowed.");
        }
        
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
            {
                _fsCache[filePath] = fileData;
            }
            return new FluentResults.Result<byte[]>().WithSuccess($"Loaded file successfully").WithValue(fileData);
        });
    }

    public virtual FluentResults.Result TrySaveXml(string filePath, in XDocument document, Encoding encoding = null) => TrySaveText(filePath, document.ToString(), encoding);
    public virtual FluentResults.Result TrySaveText(string filePath, in string text, Encoding encoding = null)
    {
        Guard.IsNotNullOrWhiteSpace(text, nameof(text));
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        if (IsWriteOperationAllowedEval?.Invoke(filePath).ConfigureAwait(false).GetAwaiter().GetResult() is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TrySaveText)}: File '{filePath}' is not allowed.");
        }
        
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


    public virtual FluentResults.Result TrySaveBinary(string filePath, in byte[] bytes)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        Guard.IsNotNull(bytes, nameof(bytes));
        Guard.HasSizeGreaterThanOrEqualTo(bytes, 1, nameof(bytes));
        using var lck = OperationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        if (IsWriteOperationAllowedEval?.Invoke(filePath).ConfigureAwait(false).GetAwaiter().GetResult() is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TrySaveBinary)}: File '{filePath}' is not allowed.");
        }
        
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

    public virtual FluentResults.Result<bool> FileExists(string filePath)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        IService.CheckDisposed(this);
        // lock not needed
        if (IsReadOperationAllowedEval?.Invoke(filePath).ConfigureAwait(false).GetAwaiter().GetResult() is not true)
        {
            return FluentResults.Result.Fail($"{nameof(FileExists)}: File '{filePath}' is not allowed.");
        }
        
        return IOExceptionsOperationRunner<bool>(nameof(FileExists), filePath, () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            return System.IO.File.Exists(fp);
        });
    }

    public virtual FluentResults.Result<bool> DirectoryExists(string directoryPath)
    {
        Guard.IsNotNullOrWhiteSpace(directoryPath, nameof(directoryPath));
        IService.CheckDisposed(this);
        // lock not needed
        if (IsReadOperationAllowedEval?.Invoke(directoryPath).ConfigureAwait(false).GetAwaiter().GetResult() is not true)
        {
            return FluentResults.Result.Fail($"{nameof(DirectoryExists)}: File '{directoryPath}' is not allowed.");
        }
        
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

    public virtual async Task<FluentResults.Result<XDocument>> TryLoadXmlAsync(string filePath, Encoding encoding = null)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        using var lck = await OperationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        if (await IsReadOperationAllowedEval.Invoke(filePath) is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TryLoadXmlAsync)}: File '{filePath}' is not allowed.");
        }
        
        if (UseCaching && _fsCache.TryGetValue(filePath, out var cachedVal) 
                       && cachedVal.TryPickT2(out var cachedDoc, out _))
        {
            return FluentResults.Result.Ok(cachedDoc);
        }
        
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

    public virtual async Task<FluentResults.Result<string>> TryLoadTextAsync(string filePath, Encoding encoding = null)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        using var lck = await OperationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        if (await IsReadOperationAllowedEval.Invoke(filePath) is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TryLoadTextAsync)}: File '{filePath}' is not allowed.");
        }
        
        if (UseCaching && _fsCache.TryGetValue(filePath, out var cachedVal) 
                       && cachedVal.TryPickT1(out var cachedTxt, out _))
        {
            return FluentResults.Result.Ok(cachedTxt);
        }
            
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

    public virtual async Task<FluentResults.Result<byte[]>> TryLoadBinaryAsync(string filePath)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        using var lck = await OperationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        if (await IsReadOperationAllowedEval.Invoke(filePath) is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TryLoadBinaryAsync)}: File '{filePath}' is not allowed.");
        }
        
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

    // method group overload
    public virtual async Task<FluentResults.Result> TrySaveXmlAsync(string filePath, XDocument document, Encoding encoding = null) => await TrySaveTextAsync(filePath, document.ToString(), encoding);
    public virtual async Task<FluentResults.Result> TrySaveTextAsync(string filePath, string text, Encoding encoding = null)
    {
        Guard.IsNotNullOrWhiteSpace(text, nameof(text));
        using var lck = await OperationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        if (await IsWriteOperationAllowedEval.Invoke(filePath) is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TrySaveTextAsync)}: File '{filePath}' is not allowed.");
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

    public virtual async Task<FluentResults.Result> TrySaveBinaryAsync(string filePath, byte[] bytes)
    {
        Guard.IsNotNullOrWhiteSpace(filePath, nameof(filePath));
        Guard.IsNotNull(bytes, nameof(bytes));
        Guard.HasSizeGreaterThanOrEqualTo(bytes, 1, nameof(bytes));
        using var lck = await OperationsLock.AcquireReaderLock();
        IService.CheckDisposed(this);
        if (await IsWriteOperationAllowedEval.Invoke(filePath) is not true)
        {
            return FluentResults.Result.Fail($"{nameof(TrySaveBinaryAsync)}: File '{filePath}' is not allowed.");
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
        catch (Exception e)
        {
            if (e is ArgumentException or ArgumentNullException)
                throw;
            return ReturnException(e, filepath).WithError(GetGeneralError(funcName, filepath));
        }
    }
    
    private async Task<FluentResults.Result> IOExceptionsOperationRunnerAsync(string funcName, string filepath, Func<Task<FluentResults.Result>> operation) 
    {
        try
        {
            return await operation?.Invoke()!;
        }
        catch (Exception e)
        {
            if (e is ArgumentException or ArgumentNullException)
                throw;
            return ReturnException(e, filepath).WithError(GetGeneralError(funcName, filepath));
        }
    }
    
    private FluentResults.Result<T> IOExceptionsOperationRunner<T>(string funcName, string filepath, Func<FluentResults.Result<T>> operation)
    {
        try
        {
            return operation?.Invoke();
        }
        catch (Exception e)
        {
            if (e is ArgumentException or ArgumentNullException)
                throw;
            return ReturnException(e, filepath).WithError(GetGeneralError(funcName, filepath));
        }
    }
    
    private FluentResults.Result IOExceptionsOperationRunner(string funcName, string filepath, Func<FluentResults.Result> operation)
    {
        try
        {
            return operation?.Invoke();
        }
        catch (Exception e)
        {
            if (e is ArgumentException or ArgumentNullException)
                throw;
            return ReturnException(e, filepath).WithError(GetGeneralError(funcName, filepath));
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
