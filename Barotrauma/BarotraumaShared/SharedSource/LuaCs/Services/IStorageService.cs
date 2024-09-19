using System.Collections.Immutable;
using System.Xml.Linq;

namespace Barotrauma.LuaCs.Services;

public interface IStorageService : IService
{
    #region LocalGameData

    bool TryLoadLocalXml(ContentPackage package, string localFilePath, out XDocument document);
    bool TryLoadLocalBinary(ContentPackage package, string localFilePath, out byte[] bytes);
    bool TryLoadLocalText(ContentPackage package, string localFilePath, out string text);
    bool FileExistsInLocalData(ContentPackage package, string localFilePath);

    #endregion
    
    #region ContentPackageData
    bool TryLoadPackageXml(ContentPackage package, string localFilePath, out XDocument document);
    bool TryLoadPackageBinary(ContentPackage package, string localFilePath, out byte[] bytes);
    bool TryLoadPackageText(ContentPackage package, string localFilePath, out string text);
    
    ImmutableArray<bool> TryLoadPackageXmlFiles(ContentPackage package, ImmutableArray<string> localFilePath, out ImmutableArray<XDocument> document);
    ImmutableArray<bool> TryLoadPackageBinaryFiles(ContentPackage package, ImmutableArray<string> localFilePath, out ImmutableArray<byte[]> bytes);
    ImmutableArray<bool> TryLoadPackageTextFiles(ContentPackage package, ImmutableArray<string> localFilePath, out ImmutableArray<string> text);

    bool FindFilesInPackage(ContentPackage package, string localSubfolder, string regexFilter, bool searchRecursively, out ImmutableArray<string> localFilePaths);
    bool FileExistsInPackage(ContentPackage package, string localFilePath);
    
    #endregion
    
    #region AbsolutePaths

    bool TryLoadXml(string filePath, out XDocument document);
    bool TrySaveXml(string filePath, in XDocument document);
    bool TryLoadBinary(string filePath, out byte[] bytes);
    bool TrySaveBinary(string filePath, in byte[] bytes);
    bool TryLoadText(string filePath, out string text);
    bool TrySaveText(string filePath, string text);
    bool FileExists(string filePath);

    #endregion
}
