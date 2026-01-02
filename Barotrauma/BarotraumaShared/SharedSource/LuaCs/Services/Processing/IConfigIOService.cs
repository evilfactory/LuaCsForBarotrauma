using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Processing;

namespace Barotrauma.LuaCs.Services.Processing;

public interface IConfigIOService : IReusableService, 
    IParserServiceAsync<IConfigResourceInfo, IReadOnlyList<IConfigInfo>>,
    IParserServiceAsync<IConfigProfileResourceInfo, IReadOnlyList<IConfigProfileInfo>>
{
    Task<FluentResults.Result> SaveConfigDataLocal(ContentPackage package, string configName, XElement serializedValue);
    Task<FluentResults.Result<OneOf.OneOf<string, XElement>>> LoadConfigDataFromLocal(ContentPackage package, string configName);
}
