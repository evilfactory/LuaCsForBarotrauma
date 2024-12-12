using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Networking;
using Barotrauma.Steam;
using FluentResults;
using FluentResults.LuaCs;
using OneOf.Types;
using Error = FluentResults.Error;
using File = Barotrauma.IO.File;
using Path = Barotrauma.IO.Path;
using Success = OneOf.Types.Success;

namespace Barotrauma.LuaCs.Services;

public class StorageService : IStorageService
{
    
    public StorageService(Lazy<IConfigService> configService)
    {
        _configService = configService;
    }
    private readonly Lazy<IConfigService> _configService;
    private IConfigEntry<string> _kLocalStoragePath = null;
    private IConfigEntry<string> _kLocalFilePathRules = null;
    private const string _packagePathKeyword = "<PACKNAME>";
    private readonly string _runLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location.CleanUpPath());
    private IConfigEntry<string> LocalStoragePath => _kLocalStoragePath ??= GetOrCreateConfig(nameof(LocalStoragePath), "/Data/Mods");
    private IConfigEntry<string> LocalFilePathRule => _kLocalFilePathRules ??= GetOrCreateConfig(nameof(LocalFilePathRule), _packagePathKeyword);
    private IConfigEntry<string> GetOrCreateConfig(string name, string defaultValue)
    {
        var c = _configService.Value
            .GetConfig<IConfigEntry<string>>(ModUtils.Definitions.LuaCsForBarotrauma, name);
        if (c.IsSuccess)
        {
            return c.Value;
        }
        else
        {
            c = _configService.Value.AddConfigEntry(
                ModUtils.Definitions.LuaCsForBarotrauma,
                name, defaultValue, NetSync.None, valueChangePredicate: (value) => false);
            if (c.IsSuccess)
                return c.Value;
            else
                throw new KeyNotFoundException("Cannot find storage value for key: " + name);
        }
    }
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        _kLocalStoragePath = null;
        _kLocalFilePathRules = null;
    }

    public FluentResults.Result<XDocument> LoadLocalXml(ContentPackage package, string localFilePath)
    {
        var r = LoadLocalText(package, localFilePath);
        if (r is { IsSuccess: true, Value: not null })
            return XDocument.Parse(r.Value);
        else
        {
            return r.ToResult<XDocument>(s => null)
                .WithError(GetGeneralError(nameof(LoadLocalXml), localFilePath, package));
        }
    }
    
        
    public FluentResults.Result<byte[]> LoadLocalBinary(ContentPackage package, string localFilePath) => TryLoadBinary(GetAbsFromLocal(package, localFilePath));
    public FluentResults.Result<string> LoadLocalText(ContentPackage package, string localFilePath) => TryLoadText(GetAbsFromLocal(package, localFilePath));
    public FluentResults.Result SaveLocalXml(ContentPackage package, string localFilePath, XDocument document) => TrySaveXml(GetAbsFromLocal(package, localFilePath), document);

    public FluentResults.Result SaveLocalBinary(ContentPackage package, string localFilePath, in byte[] bytes) => TrySaveBinary(GetAbsFromLocal(package, localFilePath), bytes);

    public FluentResults.Result SaveLocalText(ContentPackage package, string localFilePath, in string text) => TrySaveText(GetAbsFromLocal(package, localFilePath), text);

    public async Task<FluentResults.Result<XDocument>> LoadLocalXmlAsync(ContentPackage package, string localFilePath)
    {
        var r = await LoadLocalTextAsync(package, localFilePath);
        if (r is { IsSuccess: true, Value: not null })
            return XDocument.Parse(r.Value);
        else
        {
            return r.ToResult<XDocument>(s => null)
                .WithError(GetGeneralError(nameof(LoadLocalXml), localFilePath, package));
        }
    }

    public Task<FluentResults.Result<byte[]>> LoadLocalBinaryAsync(ContentPackage package, string localFilePath) => 
        TryLoadBinaryAsync(GetAbsFromLocal(package, localFilePath));
    public Task<FluentResults.Result<string>> LoadLocalTextAsync(ContentPackage package, string localFilePath) => TryLoadTextAsync(GetAbsFromLocal(package, localFilePath));
    public Task<FluentResults.Result> SaveLocalXmlAsync(ContentPackage package, string localFilePath, XDocument document) => TrySaveXmlAsync(GetAbsFromLocal(package, localFilePath), document);
    public Task<FluentResults.Result> SaveLocalBinaryAsync(ContentPackage package, string localFilePath, byte[] bytes) => TrySaveBinaryAsync(GetAbsFromLocal(package, localFilePath), bytes);
    public Task<FluentResults.Result> SaveLocalTextAsync(ContentPackage package, string localFilePath, string text) => TrySaveTextAsync(GetAbsFromLocal(package, localFilePath), text);
    public FluentResults.Result<XDocument> LoadPackageXml(ContentPackage package, string localFilePath) => TryLoadXml(Path.GetFullPath(package.Path.CleanUpPath()));
    public FluentResults.Result<byte[]> LoadPackageBinary(ContentPackage package, string localFilePath) => TryLoadBinary(Path.GetFullPath(package.Path.CleanUpPath()));
    public FluentResults.Result<string> LoadPackageText(ContentPackage package, string localFilePath) => TryLoadText(Path.GetFullPath(package.Path.CleanUpPath()));
    public FluentResults.Result<ImmutableArray<XDocument>> LoadPackageXmlFiles(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        if (localFilePaths.IsDefaultOrEmpty)
            return new FluentResults.Result<ImmutableArray<XDocument>>().WithError(new ExceptionalError(new ArgumentNullException(nameof(localFilePaths))));
        var builder = ImmutableArray.CreateBuilder<XDocument>();
        foreach (var path in localFilePaths)
        {
            if (TryLoadXml(path) is { IsSuccess: true, Value: var document })
            {
                
            }
        }
    }

    public FluentResults.Result<ImmutableArray<byte[]>> LoadPackageBinaryFiles(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        throw new NotImplementedException();
    }

    public FluentResults.Result<ImmutableArray<string>> LoadPackageTextFiles(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        ((IService)this).CheckDisposed();
        throw new NotImplementedException();
    }

    public FluentResults.Result<ImmutableArray<string>> FindFilesInPackage(ContentPackage package, string localSubfolder, string regexFilter, bool searchRecursively)
    {
        ((IService)this).CheckDisposed();
        throw new NotImplementedException();
    }

    public Task<FluentResults.Result<XDocument>> LoadPackageXmlAsync(ContentPackage package, string localFilePath)
    {
        throw new NotImplementedException();
    }

    public Task<FluentResults.Result<byte[]>> LoadPackageBinaryAsync(ContentPackage package, string localFilePath)
    {
        throw new NotImplementedException();
    }

    public Task<FluentResults.Result<string>> LoadPackageTextAsync(ContentPackage package, string localFilePath)
    {
        throw new NotImplementedException();
    }

    public Task<FluentResults.Result<ImmutableArray<XDocument>>> LoadPackageXmlFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        throw new NotImplementedException();
    }

    public Task<FluentResults.Result<ImmutableArray<byte[]>>> LoadPackageBinaryFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        throw new NotImplementedException();
    }

    public Task<FluentResults.Result<ImmutableArray<string>>> LoadPackageTextFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths)
    {
        throw new NotImplementedException();
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
        return IOExceptionsOperationRunner(nameof(TryLoadText), filePath, () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            var fileText = encoding is null ? System.IO.File.ReadAllText(fp) : System.IO.File.ReadAllText(fp, encoding);
            return new FluentResults.Result<string>().WithSuccess($"Loaded file successfully").WithValue(fileText);
        });
    }

    public FluentResults.Result<byte[]> TryLoadBinary(string filePath)
    {
        ((IService)this).CheckDisposed();
        return IOExceptionsOperationRunner(nameof(TryLoadBinary), filePath, () =>
        {
            var fp = filePath.CleanUpPath();
            fp = System.IO.Path.IsPathRooted(fp) ? fp : System.IO.Path.GetFullPath(fp);
            var fileData = System.IO.File.ReadAllBytes(fp);
            return new FluentResults.Result<byte[]>().WithSuccess($"Loaded file successfully").WithValue(fileData);
        });
    }

    public FluentResults.Result TrySaveXml(string filePath, in XDocument document, Encoding encoding = null)
    {
        ((IService)this).CheckDisposed();
        return IOExceptionsOperationRunner(nameof(TrySaveXml), filePath, () =>
        {
            
        });
    }

    public FluentResults.Result TrySaveText(string filePath, in string text, Encoding encoding = null)
    {
        ((IService)this).CheckDisposed();
        throw new NotImplementedException();
    }

    public FluentResults.Result TrySaveBinary(string filePath, in byte[] bytes)
    {
        ((IService)this).CheckDisposed();
        throw new NotImplementedException();
    }

    public FluentResults.Result<bool> FileExists(string filePath)
    {
        ((IService)this).CheckDisposed();
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result<XDocument>> TryLoadXmlAsync(string filePath, Encoding encoding = null)
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result<string>> TryLoadTextAsync(string filePath, Encoding encoding = null)
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result<byte[]>> TryLoadBinaryAsync(string filePath)
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> TrySaveXmlAsync(string filePath, XDocument document, Encoding encoding = null)
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> TrySaveTextAsync(string filePath, string text, Encoding encoding = null)
    {
        throw new NotImplementedException();
    }

    public async Task<FluentResults.Result> TrySaveBinaryAsync(string filePath, byte[] bytes)
    {
        throw new NotImplementedException();
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
    
    private Error GetGeneralError(string funcName, string localfp, ContentPackage package) =>
        new Error($"{funcName}: Failed to load local file.")
            .WithMetadata(MetadataType.ExceptionObject, this)
            .WithMetadata(MetadataType.Sources, localfp)
            .WithMetadata(MetadataType.RootObject, package);
    
    private Error GetGeneralError(string funcName, string localfp) =>
        new Error($"{funcName}: Failed to load local file.")
            .WithMetadata(MetadataType.ExceptionObject, this)
            .WithMetadata(MetadataType.Sources, localfp);

    private string GetAbsFromLocal(ContentPackage package, string localFilePath)
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(
            _runLocation,
            LocalStoragePath.Value,
            LocalFilePathRule.Value.Replace(_packagePathKeyword, package.Name.IsNullOrWhiteSpace()
                ? package.TryExtractSteamWorkshopId(out var id)
                    ? id.Value.ToString()
                    : "_fallbackFolder"
                : package.Name),
            localFilePath));
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
