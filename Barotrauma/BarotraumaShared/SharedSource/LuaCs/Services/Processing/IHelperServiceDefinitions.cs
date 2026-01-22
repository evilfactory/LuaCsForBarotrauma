using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services.Processing;

public interface IParserService<in TSrc, TOut> : IService
{
    Result<TOut> TryParseResource(TSrc src);
    ImmutableArray<Result<TOut>> TryParseResources(IEnumerable<TSrc> sources);
}

public interface IParserServiceAsync<in TSrc, TOut> : IService
{
    Task<Result<TOut>> TryParseResourceAsync(TSrc src);
    Task<ImmutableArray<Result<TOut>>> TryParseResourcesAsync(IEnumerable<TSrc> sources);
}

public interface IParserServiceOneToManyAsync<in TSrc, TOut> : IService
{
    Task<Result<ImmutableArray<TOut>>> TryParseResourcesAsync(TSrc src);
}
