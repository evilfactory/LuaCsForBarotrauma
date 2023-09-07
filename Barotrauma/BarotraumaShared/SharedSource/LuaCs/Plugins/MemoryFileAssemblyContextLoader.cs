using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using Barotrauma;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Barotrauma;

/// <summary>
/// AssemblyLoadContext to compile from syntax trees in memory and to load from disk/file. Provides dependency resolution.
/// [IMPORTANT] Only supports 1 in-memory compiled assembly at a time. Use more instances if you need more.
/// [IMPORTANT] All file assemblies required for the compilation of syntax trees should be loaded first.
/// </summary>
public class MemoryFileAssemblyContextLoader : AssemblyLoadContext
{
    // public
    // ReSharper disable MemberCanBePrivate.Global
    public Assembly CompiledAssembly { get; private set; } = null;
    public byte[] CompiledAssemblyImage { get; private set; } = null;
    // ReSharper restore MemberCanBePrivate.Global 
    // internal
    private readonly Dictionary<string, AssemblyDependencyResolver> _dependencyResolvers = new();       // path-folder, resolver
    protected bool IsResolving;   //this is to avoid circular dependency lookup.
    private AssemblyManager _assemblyManager;
    public bool IsTemplateMode { get; set; } = false;
    
    public MemoryFileAssemblyContextLoader(AssemblyManager assemblyManager) : base(isCollectible: true)
    {
        this._assemblyManager = assemblyManager;
    }
    

    /// <summary>
    /// Try to load the list of disk-file assemblies.
    /// </summary>
    /// <param name="assemblyFilePaths">Operation success or failure reason.</param>
    public AssemblyLoadingSuccessState LoadFromFiles([NotNull] IEnumerable<string> assemblyFilePaths)
    {
        if (assemblyFilePaths is null)
            throw new ArgumentNullException(
                $"{nameof(MemoryFileAssemblyContextLoader)}::{nameof(LoadFromFiles)}() | The supplied filepath list is null.");
        
        foreach (string filepath in assemblyFilePaths)
        {
            // path verification
            if (filepath.IsNullOrWhiteSpace())
                continue;
            string sanitizedFilePath = System.IO.Path.GetFullPath(filepath.CleanUpPath());
            string directoryKey = System.IO.Path.GetDirectoryName(sanitizedFilePath);

            if (directoryKey is null)
                return AssemblyLoadingSuccessState.BadFilePath;

            // setup dep resolver if not available
            if (!_dependencyResolvers.ContainsKey(directoryKey) || _dependencyResolvers[directoryKey] is null)
            {
                _dependencyResolvers[directoryKey] = new AssemblyDependencyResolver(sanitizedFilePath); // supply the first assembly to be loaded
            }
            
            // try loading the assemblies
            try
            {
                LoadFromAssemblyPath(sanitizedFilePath);
            }
            // on fail of any we're done because we assume that loaded files are related. This ACL needs to be unloaded and collected.
            catch (ArgumentNullException ane)
            {
                return AssemblyLoadingSuccessState.BadFilePath;
            }
            catch (ArgumentException ae)
            {
                return AssemblyLoadingSuccessState.BadFilePath;
            }
            catch (FileLoadException fle)
            {
                return AssemblyLoadingSuccessState.CannotLoadFile;
            }
            catch (FileNotFoundException fne)
            {
                return AssemblyLoadingSuccessState.NoAssemblyFound;
            }
            catch (BadImageFormatException bfe)
            {
                return AssemblyLoadingSuccessState.InvalidAssembly;
            }
            catch (Exception e)
            {
#if SERVER
                LuaCsLogger.LogError($"Unable to load dependency assembly file at {filepath.CleanUpPath()} for the assembly named {CompiledAssembly?.FullName}. | Data: {e.Message} | InnerException: {e.InnerException}");
#elif CLIENT
                LuaCsLogger.ShowErrorOverlay($"Unable to load dependency assembly file at {filepath} for the assembly named {CompiledAssembly?.FullName}. | Data: {e.Message} | InnerException: {e.InnerException}");
#endif
                return AssemblyLoadingSuccessState.ACLLoadFailure;
            }
        }

        return AssemblyLoadingSuccessState.Success;
    }


    /// <summary>
    /// Compiles the supplied syntaxtrees and options into an in-memory assembly image.
    /// Builds metadata from loaded assemblies, only supply your own if you have in-memory images not managed by the
    /// AssemblyManager class. 
    /// </summary>
    /// <param name="assemblyName">Name of the assembly. Must be supplied for in-memory assemblies.</param>
    /// <param name="syntaxTrees">Syntax trees to compile into the assembly.</param>
    /// <param name="externMetadataReferences">Metadata to be used for compilation.
    /// [IMPORTANT] This method builds metadata from loaded assemblies, only supply your own if you have in-memory
    /// images not managed by the AssemblyManager class.</param>
    /// <param name="compilationOptions">CSharp compilation options. This method automatically adds the 'IgnoreAccessChecks' property for compilation.</param>
    /// <param name="compilationMessages">Will contain any diagnostic messages for compilation failure.</param>
    /// <param name="externFileAssemblyReferences">Additional assemblies located in the FileSystem to build metadata references from.
    /// Assemblies here will have duplicates by the same name that are currently loaded filtered out.</param>
    /// <returns>Success state of the operation.</returns>
    /// <exception cref="ArgumentNullException">Throws exception if any of the required arguments are null.</exception>
    public AssemblyLoadingSuccessState CompileAndLoadScriptAssembly(
        [NotNull] string assemblyName,
        [NotNull] IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> externMetadataReferences,
        [NotNull] CSharpCompilationOptions compilationOptions,
        out string compilationMessages,
        IEnumerable<Assembly> externFileAssemblyReferences = null)
    {
        compilationMessages = "";

        if (this.CompiledAssembly is not null)
        {
            return AssemblyLoadingSuccessState.AlreadyLoaded;
        }

        var externAssemblyRefs = externFileAssemblyReferences is not null ? externFileAssemblyReferences.ToImmutableList() : ImmutableList<Assembly>.Empty;
        var externAssemblyNames = externAssemblyRefs.Any() ? externAssemblyRefs
                .Where(a => a.FullName is not null)
                .Select(a => a.FullName).ToImmutableHashSet() 
            : ImmutableHashSet<string>.Empty;

        // verifications
        if (assemblyName.IsNullOrWhiteSpace())
            throw new ArgumentNullException(
                $"{nameof(MemoryFileAssemblyContextLoader)}::{nameof(CompileAndLoadScriptAssembly)}() | The supplied assembly name is null!");

        if (syntaxTrees is null)
            throw new ArgumentNullException(
                $"{nameof(MemoryFileAssemblyContextLoader)}::{nameof(CompileAndLoadScriptAssembly)}() | The supplied syntax tree is null!");
        
        // add external references
        List<MetadataReference> metadataReferences = new();
        if (externMetadataReferences is not null)
            metadataReferences.AddRange(externMetadataReferences);

        // build metadata refs from global where not an in-memory compiled assembly and not the same assembly as supplied.
        metadataReferences.AddRange(AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                if (a.IsDynamic || string.IsNullOrEmpty(a.Location) || a.Location.Contains("xunit"))
                    return false;
                if (a.FullName is null)
                    return true;
                return !externAssemblyNames.Contains(a.FullName);    // exclude duplicates
            })
            .Select(a => MetadataReference.CreateFromFile(a.Location) as MetadataReference)
            .Union(externAssemblyRefs   // add custom supplied assemblies
                .Where(a => !(a.IsDynamic || string.IsNullOrEmpty(a.Location) || a.Location.Contains("xunit")))
                .Select(a => MetadataReference.CreateFromFile(a.Location) as MetadataReference)
            ).ToList());
            
        // build metadata refs from in-memory images
        foreach (var loadedAcl in _assemblyManager.GetAllLoadedACLs())
        {
            if (loadedAcl.Acl.CompiledAssemblyImage is null || loadedAcl.Acl.CompiledAssemblyImage.Length == 0)
                continue;
            metadataReferences.Add(MetadataReference.CreateFromImage(loadedAcl.Acl.CompiledAssemblyImage));
        }
        
        // Change inaccessible options to allow public access to restricted members
        var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
        topLevelBinderFlagsProperty?.SetValue(compilationOptions, (uint)1 << 22);
        
        // begin compilation 
        using var memoryCompilation = new MemoryStream();
        // compile, emit
        var result = CSharpCompilation.Create(assemblyName, syntaxTrees, metadataReferences, compilationOptions).Emit(memoryCompilation);
        // check for errors
        if (!result.Success)
        {
            IEnumerable<Diagnostic> failures = result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error);
            foreach (Diagnostic diagnostic in failures)
            {
                compilationMessages += $"\n{diagnostic}";
            }

            return AssemblyLoadingSuccessState.InvalidAssembly;
        }

        // read compiled assembly from memory stream into an in-memory assembly & image
        memoryCompilation.Seek(0, SeekOrigin.Begin);   // reset
        try
        {
            CompiledAssembly = LoadFromStream(memoryCompilation);
            CompiledAssemblyImage = memoryCompilation.ToArray();
        }
        catch (Exception e)
        {
#if SERVER
            LuaCsLogger.LogError($"Unable to load memory assembly from stream. | Data: {e.Message} | InnerException: {e.InnerException}");
#elif CLIENT
            LuaCsLogger.ShowErrorOverlay($"Unable to load memory assembly from stream. | Data: {e.Message} | InnerException: {e.InnerException}");
#endif
            return AssemblyLoadingSuccessState.CannotLoadFromStream;
        }

        return AssemblyLoadingSuccessState.Success;
    }
    
    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
    protected override Assembly Load(AssemblyName assemblyName)
    {
        if (IsResolving)
            return null;    //circular resolution fast exit.

        try
        {
            IsResolving = true;

            // resolve self collection
            Assembly ass = this.Assemblies.FirstOrDefault(a =>
                a.FullName is not null && a.FullName.Equals(assemblyName.FullName), null);
            if (ass is not null)
                return ass;

            // resolve to local folders
            foreach (KeyValuePair<string,AssemblyDependencyResolver> pair in _dependencyResolvers)
            {
                var asspath = pair.Value.ResolveAssemblyToPath(assemblyName);
                if (asspath is null)
                    continue;
                ass = LoadFromAssemblyPath(asspath);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (ass is not null)
                    return ass;
            }

            //try resolve against other loaded alcs
            foreach (var loadedAcL in _assemblyManager.GetAllLoadedACLs())
            {
                if (loadedAcL.Acl is null || loadedAcL.Acl.IsTemplateMode) continue;
                
                try
                {
                    ass = loadedAcL.Acl.LoadFromAssemblyName(assemblyName);
                    if (ass is not null)
                        return ass;
                }
                catch
                {
                    // LoadFromAssemblyName throws, no need to propagate
                }
            }
            
            ass = AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
            if (ass is not null)
                return ass;
        }
        finally
        {
            IsResolving = false;
        }
        
        return null;
    }
    

    private new void Unload()
    {
        CompiledAssembly = null;
        CompiledAssemblyImage = null;
        base.Unload();
    }
}
