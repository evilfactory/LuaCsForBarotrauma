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

public interface IAssemblyManagementService : IReusableService
{

    /// <summary>
    /// Searches for an assembly given it's fully qualified name, while excluding the contexts with the given Guids, if supplied.
    /// </summary>
    /// <param name="assemblyName">The assembly info.</param>
    /// <param name="excludedContexts">Guids of excluded contexts.</param>
    /// <returns><b>On Success:</b> The assembly. <br/><b>On Failure:</b> nothing.</returns>
    FluentResults.Result<Assembly> GetLoadedAssembly(OneOf<AssemblyName, string> assemblyName, in Guid[] excludedContexts);
    
    /// <summary>
    /// Gets all <see cref="MetadataReference"/> for all service-managed assemblies.
    /// </summary>
    /// <returns><see cref="MetadataReference"/> collection for all service-managed, and default if selected, assemblies, if any are found. Returns an empty collection otherwise.</returns>
    ImmutableArray<MetadataReference> GetDefaultMetadataReferences(bool includeDefaultContext = true);
    
    /// <summary>
    /// Returns all active, managed assembly loaders.
    /// </summary>
    ImmutableArray<IAssemblyLoaderService> AssemblyLoaderServices { get; }
    
    
}
