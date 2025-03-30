using System;
using System.Collections.Immutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Barotrauma.LuaCs.Services;

public interface IStorageService : IService
{
    
    bool UseCaching { get; set; }
    
    /// <summary>
    /// Deletes all cached file data.
    /// </summary>
    void PurgeCache();
    
    /// <summary>
    /// Deletes the data for the supplied file path from the data cache.
    /// </summary>
    /// <param name="absolutePath"></param>
    void PurgeFileFromCache(string absolutePath);
    
    /// <summary>
    /// Deletes the data from the supplied file paths from the data cache.
    /// </summary>
    /// <param name="absolutePaths"></param>
    void PurgeFilesFromCache(params string[] absolutePaths);
    
    // -- local game folder storage
    FluentResults.Result<XDocument> LoadLocalXml(ContentPackage package, string localFilePath);
    FluentResults.Result<byte[]> LoadLocalBinary(ContentPackage package, string localFilePath);
    FluentResults.Result<string> LoadLocalText(ContentPackage package, string localFilePath);
    FluentResults.Result SaveLocalXml(ContentPackage package, string localFilePath, XDocument document);
    FluentResults.Result SaveLocalBinary(ContentPackage package, string localFilePath, in byte[] bytes);
    FluentResults.Result SaveLocalText(ContentPackage package, string localFilePath, in string text);
    // async
    Task<FluentResults.Result<XDocument>> LoadLocalXmlAsync(ContentPackage package, string localFilePath);
    Task<FluentResults.Result<byte[]>> LoadLocalBinaryAsync(ContentPackage package, string localFilePath);
    Task<FluentResults.Result<string>> LoadLocalTextAsync(ContentPackage package, string localFilePath);
    Task<FluentResults.Result> SaveLocalXmlAsync(ContentPackage package, string localFilePath, XDocument document);
    Task<FluentResults.Result> SaveLocalBinaryAsync(ContentPackage package, string localFilePath, byte[] bytes);
    Task<FluentResults.Result> SaveLocalTextAsync(ContentPackage package, string localFilePath, string text);
    
    // -- package directory
    // singles
    FluentResults.Result<XDocument> LoadPackageXml(ContentPackage package, string localFilePath);
    FluentResults.Result<byte[]> LoadPackageBinary(ContentPackage package, string localFilePath);
    FluentResults.Result<string> LoadPackageText(ContentPackage package, string localFilePath);
    // collections
    ImmutableArray<(string, FluentResults.Result<XDocument>)> LoadPackageXmlFiles(ContentPackage package, ImmutableArray<string> localFilePaths);
    ImmutableArray<(string, FluentResults.Result<byte[]>)> LoadPackageBinaryFiles(ContentPackage package, ImmutableArray<string> localFilePaths);
    ImmutableArray<(string, FluentResults.Result<string>)> LoadPackageTextFiles(ContentPackage package, ImmutableArray<string> localFilePaths);
    FluentResults.Result<ImmutableArray<string>> FindFilesInPackage(ContentPackage package, string localSubfolder, string regexFilter, bool searchRecursively);
    FluentResults.Result<string> GetAbsFromPackage(ContentPackage package, string localFilePath);
    // async
    // singles
    Task<FluentResults.Result<XDocument>> LoadPackageXmlAsync(ContentPackage package, string localFilePath);
    Task<FluentResults.Result<byte[]>> LoadPackageBinaryAsync(ContentPackage package, string localFilePath);
    Task<FluentResults.Result<string>> LoadPackageTextAsync(ContentPackage package, string localFilePath);
    // collections
    Task<ImmutableArray<(string, FluentResults.Result<XDocument>)>> LoadPackageXmlFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths);
    Task<ImmutableArray<(string, FluentResults.Result<byte[]>)>> LoadPackageBinaryFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths);
    Task<ImmutableArray<(string, FluentResults.Result<string>)>> LoadPackageTextFilesAsync(ContentPackage package, ImmutableArray<string> localFilePaths);
    
    // -- absolute paths
    FluentResults.Result<XDocument> TryLoadXml(string filePath, Encoding encoding = null);
    FluentResults.Result<string> TryLoadText(string filePath, Encoding encoding = null);
    FluentResults.Result<byte[]> TryLoadBinary(string filePath);
    FluentResults.Result TrySaveXml(string filePath, in XDocument document, Encoding encoding = null);
    FluentResults.Result TrySaveText(string filePath, in string text, Encoding encoding = null);
    FluentResults.Result TrySaveBinary(string filePath, in byte[] bytes);
    FluentResults.Result<bool> FileExists(string filePath);
    FluentResults.Result<bool> DirectoryExists(string directoryPath);
    
    //async
    Task<FluentResults.Result<XDocument>> TryLoadXmlAsync(string filePath, Encoding encoding = null);
    Task<FluentResults.Result<string>> TryLoadTextAsync(string filePath, Encoding encoding = null);
    Task<FluentResults.Result<byte[]>> TryLoadBinaryAsync(string filePath);
    Task<FluentResults.Result> TrySaveXmlAsync(string filePath, XDocument document, Encoding encoding = null);
    Task<FluentResults.Result> TrySaveTextAsync(string filePath, string text, Encoding encoding = null);
    Task<FluentResults.Result> TrySaveBinaryAsync(string filePath, byte[] bytes);
}
