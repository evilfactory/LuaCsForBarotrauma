using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IContentPackageService : IService
{
    bool TryParsePackage(ContentPackage package);
    ContentPackage Package { get; }
    IModConfigInfo ModConfigInfo { get; }
}

