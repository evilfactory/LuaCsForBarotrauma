using System;
using System.Collections.Generic;

namespace Barotrauma.LuaCs.Services;

public interface ILocalizationService : IService
{
    bool TryLoadFiles(in string[] filePaths);
    void UnloadAll();
    string GetLocalizedString(string key, string fallback);
    string GetLocalizedString(string key, Func<string, string> fbValueFactory);
}
