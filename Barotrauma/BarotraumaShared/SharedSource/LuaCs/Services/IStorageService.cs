using System.Xml.Linq;

namespace Barotrauma.LuaCs.Services;

public interface IStorageService : IService
{
    bool TryLoadXml(string filePath, out XDocument document);
    bool TrySaveXml(string filePath, in XDocument document);
    bool TryLoadBinary(string filePath, out byte[] bytes);
    bool TrySaveBinary(string filePath, in byte[] bytes);
    bool TryLoadText(string filePath, out string text);
    bool TrySaveText(string filePath, string text);
    bool FileExists(string filePath);
}
