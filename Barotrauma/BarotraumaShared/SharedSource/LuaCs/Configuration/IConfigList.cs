using System.Collections.Generic;
using Barotrauma.LuaCs.Services;

namespace Barotrauma.LuaCs.Configuration;

public interface IConfigList : IConfigBase, INetworkSyncEntity
{
    IReadOnlyList<string> Options { get; }
}
