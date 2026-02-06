using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs;
using FluentResults;

namespace Barotrauma.LuaCs;

public interface IModConfigService :  IService
{
    /// <summary>
    /// Loads or dynamically generates a <see cref="IModConfigInfo"/> for the given <see cref="ContentPackage"/>.
    /// <br/> Throws a <see cref="NullReferenceException"/> if the package is null.
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    Task<Result<IModConfigInfo>> CreateConfigAsync([NotNull]ContentPackage src);
    Task<ImmutableArray<(ContentPackage Source, Result<IModConfigInfo> Config)>> CreateConfigsAsync(ImmutableArray<ContentPackage> src);
}
