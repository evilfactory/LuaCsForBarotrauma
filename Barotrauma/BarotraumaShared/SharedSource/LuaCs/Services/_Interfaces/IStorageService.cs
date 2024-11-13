using System.Collections.Immutable;
using System.Xml.Linq;

namespace Barotrauma.LuaCs.Services;

public interface IStorageService : IReusableService
{
    #region LocalGameData

    FluentResults.Result<XDocument> LoadLocalXml(ContentPackage package, string localFilePath);
    FluentResults.Result<byte[]> LoadLocalBinary(ContentPackage package, string localFilePath);
    FluentResults.Result<string> LoadLocalText(ContentPackage package, string localFilePath);
    FluentResults.Result<bool> FileExistsInLocalData(ContentPackage package, string localFilePath);

    #endregion
    
    #region ContentPackageData
    FluentResults.Result<XDocument> LoadPackageXml(ContentPackage package, string localFilePath, out XDocument document);
    FluentResults.Result<byte[]> LoadPackageBinary(ContentPackage package, string localFilePath, out byte[] bytes);
    FluentResults.Result<string> LoadPackageText(ContentPackage package, string localFilePath, out string text);
    
    FluentResults.Result<ImmutableArray<XDocument>> LoadPackageXmlFiles(ContentPackage package, ImmutableArray<string> localFilePath);
    FluentResults.Result<ImmutableArray<byte[]>> TryLoadPackageBinaryFiles(ContentPackage package, ImmutableArray<string> localFilePath);
    FluentResults.Result<ImmutableArray<string>> TryLoadPackageTextFiles(ContentPackage package, ImmutableArray<string> localFilePath);

    FluentResults.Result<ImmutableArray<string>> FindFilesInPackage(ContentPackage package, string localSubfolder, string regexFilter, bool searchRecursively);
    FluentResults.Result<bool> FileExistsInPackage(ContentPackage package, string localFilePath);
    
    #endregion
    
    #region AbsolutePaths

    FluentResults.Result<XDocument> TryLoadXml(string filePatht);
    FluentResults.Result TrySaveXml(string filePath, in XDocument document);
    FluentResults.Result<byte[]> TryLoadBinary(string filePath);
    FluentResults.Result TrySaveBinary(string filePath, in byte[] bytes);
    FluentResults.Result<string> TryLoadText(string filePath);
    FluentResults.Result TrySaveText(string filePath, string text);
    FluentResults.Result<bool> FileExists(string filePath);

    #endregion
}
