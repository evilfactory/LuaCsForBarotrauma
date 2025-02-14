using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using FluentResults;

namespace Barotrauma.LuaCs.Services.Processing;

#region TypeDef

public interface IConverterService<in TSrc, TOut> : IReusableService
{
    Result<TOut> TryParseResource(TSrc src);
    Result<TOut> TryParseResources(IEnumerable<TSrc> sources);
}

public interface IConverterServiceAsync<in TSrc, TOut> : IReusableService
{
    Task<Result<TOut>> TryParseResourceAsync(TSrc src);
    Task<Result<TOut>> TryParseResourcesAsync(IEnumerable<TSrc> sources);
}

#endregion
