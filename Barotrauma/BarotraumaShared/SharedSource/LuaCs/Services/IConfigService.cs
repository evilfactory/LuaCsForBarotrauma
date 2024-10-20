using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Services;

public interface IConfigService : IService
{
    bool TryAddConfigs(ImmutableArray<IConfigResourceInfo> configResources);
    bool TryAddConfigsProfiles(ImmutableArray<IConfigProfileResourceInfo> configProfileResources);
    void RemoveConfigs(ImmutableArray<IConfigResourceInfo> configResources);
    void RemoveConfigsProfiles(ImmutableArray<IConfigProfileResourceInfo> configProfilesResources);
}
