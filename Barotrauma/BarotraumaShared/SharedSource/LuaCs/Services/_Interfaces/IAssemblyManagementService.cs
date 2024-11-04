using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
// ReSharper disable InconsistentNaming

namespace Barotrauma.LuaCs.Services;

public interface IAssemblyManagementService : IReusableService
{
    /// <summary>
    /// Searches for an assembly given it's fully qualified name, while excluding the contexts with the given Guids, if supplied.
    /// </summary>
    /// <param name="assemblyName">The fully-qualified assembly name.</param>
    /// <param name="excludedContexts">Guids of excluded contexts.</param>
    /// <returns><b>On Success:</b> The assembly. <br/><b>On Failure:</b> nothing.</returns>
    FluentResults.Result<Assembly> GetLoadedAssembly(string assemblyName, in Guid[] excludedContexts);
    /// <summary>
    /// Searches for an assembly given it's fully qualified name, while excluding the contexts with the given Guids, if supplied.
    /// </summary>
    /// <param name="assemblyName">The assembly info.</param>
    /// <param name="excludedContexts">Guids of excluded contexts.</param>
    /// <returns><b>On Success:</b> The assembly. <br/><b>On Failure:</b> nothing.</returns>
    FluentResults.Result<Assembly> GetLoadedAssembly(AssemblyName assemblyName, in Guid[] excludedContexts);
    
    /// <summary>
    /// Gets the assembly <see cref="MetadataReference"/> collection for the BCL and base game assemblies. 
    /// </summary>
    /// <returns><see cref="MetadataReference"/> collection, if any are found. Returns an empty collection otherwise.</returns>
    ImmutableArray<MetadataReference> GetDefaultMetadataReferences();
    
    /// <summary>
    /// Gets the assembly <see cref="MetadataReference"/> collection for all add-in assemblies loaded.
    /// </summary>
    /// <returns><see cref="MetadataReference"/> collection, if any are found. Returns an empty collection otherwise.</returns>
    ImmutableArray<MetadataReference> GetAddInContextsMetadataReferences();
    
    /// <summary>
    /// 
    /// </summary>
    ImmutableArray<IAssemblyLoaderService> AssemblyLoaderServices { get; }
}
