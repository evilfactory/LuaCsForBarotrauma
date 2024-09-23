using System.Collections.Generic;

namespace Barotrauma.LuaCs.Services.Processing;

public interface IResourceParser<TSrc,TOut>
{
    bool TryParseResource(in TSrc src, out TOut resources);
    bool TryParseResources(in IEnumerable<TSrc> sources, out List<TOut> resources);
}
