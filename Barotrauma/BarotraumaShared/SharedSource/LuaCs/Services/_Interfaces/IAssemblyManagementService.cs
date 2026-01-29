using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OneOf;

// ReSharper disable InconsistentNaming

namespace Barotrauma.LuaCs.Services;

public interface IAssemblyManagementService : IPluginManagementService
{

    /// <summary>
    /// Searches for an assembly given it's fully qualified name, while excluding the contexts with the given Guids, if supplied.
    /// </summary>
    /// <param name="assemblyName">The assembly info.</param>
    /// <param name="excludedContexts">Guids of excluded contexts.</param>
    /// <returns><b>On Success:</b> The assembly. <br/><b>On Failure:</b> nothing.</returns>
    FluentResults.Result<Assembly> GetLoadedAssembly(OneOf<AssemblyName, string> assemblyName, in Guid[] excludedContexts);
}
