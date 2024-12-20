using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Barotrauma.LuaCs.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Barotrauma.LuaCs;

public interface IAssemblyLoaderService : IService
{
    /// <summary>
    /// Assembly loader factory for DI registration.
    /// </summary>
    /// <param name="assemblyManagementService">The assembly hosting management service.</param>
    /// <param name="eventService">The event service for publishing.</param>
    /// <param name="id">The referencing ID. Intended to be used to distinguish between instances.</param>
    /// <param name="name">The name of the friendly name instance, used for error messages.</param>
    /// <param name="isReferenceOnlyMode">Loaded assemblies are not intended for execution, just MetadataReferences.</param>
    delegate IAssemblyLoaderService AssemblyLoaderDelegate(
        IAssemblyManagementService assemblyManagementService, 
        IEventService eventService, Guid id, string name, 
        bool isReferenceOnlyMode, Action<AssemblyLoader> onUnload);
    
    /// <summary>
    /// ID for this instance. 
    /// </summary>
    Guid Id { get; }
    /// <summary>
    /// Indicates that the assemblies in this load context are metadata references only and not
    /// intended for execution.
    /// </summary>
    bool IsReferenceOnlyMode { get; }
    /// <summary>
    /// Runtime value of constant <see cref="InternalsAwareAssemblyName"/> for extensibility use.
    /// </summary>
    public static readonly string InternalsAccessAssemblyName = InternalsAwareAssemblyName;
    /// <summary>
    /// Name for all runtime-compiled assemblies requiring access to <c>internal</c> assembly components. <seealso cref="InternalsVisibleToAttribute"/>
    /// </summary>
    public const string InternalsAwareAssemblyName = "InternalsAwareAssembly";

    /// <summary>
    /// Add additional locations for dependency resolution to use.
    /// </summary>
    /// <param name="paths"></param>
    /// <returns></returns>
    public FluentResults.Result AddDependencyPaths(ImmutableArray<string> paths);

    /// <summary>
    /// Compiles the supplied syntaxtrees and options into an in-memory assembly image.
    /// Builds metadata from loaded assemblies, only supply your own if you have in-memory images not managed by the
    /// AssemblyManager class. 
    /// </summary>
    /// <param name="assemblyName"><c>[NotNull]</c>Name reference of the assembly.
    /// <para><b>[IMPORTANT]</b> This is used to reference this assembly as the true name will be forced if
    /// publicized assemblies are not used (InternalsVisibleTo Attrib).</para>
    /// Must be supplied for in-memory assemblies.
    /// <para>Must be unique to all other assemblies explicitly loaded using this context.</para></param>
    /// <param name="compileWithInternalAccess">Forces the assembly name to <see cref="InternalsAccessAssemblyName"/> and grants access to <c>internal</c>.</param>
    /// <para><b>[IMPORTANT]</b>Cannot be null or empty if <see cref="compileWithInternalAccess"/> is false.</para></param>
    /// <param name="syntaxTrees"><c>[NotNull]</c>Syntax trees to compile into the assembly.</param>
    /// <param name="metadataReferences">All <c>MetadataReference<c/>s to be used for compilation.
    /// [IMPORTANT] This method builds metadata from loaded assemblies, only supply your own if you have in-memory
    /// images not managed by the AssemblyManager class.</param>
    /// <param name="compilationOptions"><c>[NotNull]</c>CSharp compilation options. This method automatically adds the 'IgnoreAccessChecks' property for compilation.</param>
    /// <returns>Success state of the operation.</returns>
    public FluentResults.Result<Assembly> CompileScriptAssembly(
        [NotNull] string assemblyName,
        bool compileWithInternalAccess,
        ImmutableArray<SyntaxTree> syntaxTrees,
        ImmutableArray<MetadataReference> metadataReferences,
        CSharpCompilationOptions compilationOptions = null);

    /// <summary>
    /// Loads the assembly from the provided location and registers all new paths provided with dependency resolution.
    /// </summary>
    /// <param name="assemblyFilePath">Absolute path to the managed assembly.</param>
    /// <param name="additionalDependencyPaths">Additional paths for dependency resolution.</param>
    /// <returns>Success and reference to the assembly if successful.</returns>
    public FluentResults.Result<Assembly> LoadAssemblyFromFile(string assemblyFilePath,
        ImmutableArray<string> additionalDependencyPaths);

    /// <summary>
    /// Returns the already loaded assembly with the same name.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly.</param>
    /// <returns>Operation success on assembly found and assembly.</returns>
    public FluentResults.Result<Assembly> GetAssemblyByName(string assemblyName);

    /// <summary>
    /// Gets the list of <c>Type</c>s from loaded assemblies.
    /// </summary>
    /// <returns></returns>
    public FluentResults.Result<ImmutableArray<Type>> GetTypesInAssemblies();
    
    /// <summary>
    /// Gets the list of <c>Type</c>s from loaded assemblies. Does not create a defensive copy and blocks loading/unloading.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Type> UnsafeGetTypesInAssemblies();
    
    /// <summary>
    /// Returns the first found type given it's fully qualified name.
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public FluentResults.Result<Type> GetTypeInAssemblies(string typeName);
    
    /// <summary>
    /// List of loaded assemblies.
    /// </summary>
    public IEnumerable<Assembly> Assemblies { get; }
    
    public IEnumerable<MetadataReference> AssemblyReferences { get; }
}


