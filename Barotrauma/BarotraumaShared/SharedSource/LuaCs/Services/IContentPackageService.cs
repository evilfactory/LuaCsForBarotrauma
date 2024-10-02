using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IContentPackageService : IService
{
    /// <summary>
    /// Tries to parse a package to produce a working ModConfigInfo.
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    bool TryParsePackage(ContentPackage package);
    ContentPackage Package { get; }
    IModConfigInfo ModConfigInfo { get; }
}

