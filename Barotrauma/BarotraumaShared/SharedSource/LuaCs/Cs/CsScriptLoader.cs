using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Barotrauma
{
    class CsScriptLoader : CsScriptBase
    {
        private static Dictionary<string, string> _dirToModNameCache = new();
        private List<MetadataReference> defaultReferences;

        private Dictionary<string, List<string>> sources;
        public List<Assembly> LoadedAssemblies { get; } = new();
        private PublicizedBinariesResolver publicizedBinariesResolver;

        public CsScriptLoader()
        {
            publicizedBinariesResolver = new PublicizedBinariesResolver();
            defaultReferences = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !(a.IsDynamic || string.IsNullOrEmpty(a.Location) || a.Location.Contains("xunit")))
                .Select(a => MetadataReference.CreateFromFile(this.publicizedBinariesResolver.TryFindPublicized(a.Location)) as MetadataReference)
                .ToList();

            sources = new Dictionary<string, List<string>>();
        }

        private enum RunType { Standard, Forced, None };
        private bool ShouldRun(ContentPackage cp, string path)
        {
            if (!Directory.Exists(path + "CSharp"))
            {
                return false;
            }

            var isEnabled = ContentPackageManager.EnabledPackages.All.Contains(cp);
            if (File.Exists(path + "CSharp/RunConfig.xml"))
            {
                Stream stream = File.Open(path + "CSharp/RunConfig.xml", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var doc = XDocument.Load(stream);
                var elems = doc.Root.Elements().ToArray();
                var elem = elems.FirstOrDefault(e => e.Name.LocalName.Equals(LuaCsSetup.IsServer ? "Server" : (LuaCsSetup.IsClient ? "Client" : "None"), StringComparison.OrdinalIgnoreCase));

                if (elem != null && Enum.TryParse(elem.Value, true, out RunType rtValue))
                {
                    if (rtValue == RunType.Standard && isEnabled)
                    {
                        LuaCsLogger.LogMessage($"Added {cp.Name} {cp.ModVersion} to Cs compilation. (Standard)");
                        return true;
                    }
                    else if (rtValue == RunType.Forced && (isEnabled || !GameMain.LuaCs.Config.TreatForcedModsAsNormal))
                    {
                        LuaCsLogger.LogMessage($"Added {cp.Name} {cp.ModVersion} to Cs compilation. (Forced)");
                        return true;
                    }
                    else if (rtValue == RunType.None)
                    {
                        return false;
                    }
                }

                stream.Close();
            }

            if (isEnabled)
            {
                LuaCsLogger.LogMessage($"Added {cp.Name} {cp.ModVersion} to Cs compilation. (Assumed)");
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SearchFolders()
        {
            var packagesAdded = new HashSet<ContentPackage>();
            var paths = new Dictionary<string, string>();
            foreach (var cp in ContentPackageManager.AllPackages.Concat(ContentPackageManager.EnabledPackages.All))
            {
                if (packagesAdded.Contains(cp)) { continue; }
                var path = $"{Path.GetFullPath(Path.GetDirectoryName(cp.Path)).Replace('\\', '/')}/";
                if (ShouldRun(cp, path))
                {
                    if (paths.ContainsKey(cp.Name))
                    {
                        if (ContentPackageManager.EnabledPackages.All.Contains(cp))
                        {
                            paths[cp.Name] = path;
                        }
                    }
                    else
                    {
                        paths.Add(cp.Name, path);
                    }
                    packagesAdded.Add(cp);
                }
            }

            foreach ((var _, var path) in paths)
            {
                RunFolder(path);
            }
        }

        public bool HasSources { get => sources.Count > 0; }

        private void AddSources(string modRoot, string srcRoot)
        {
            var name = GetModAssemblyName(modRoot);
            
            foreach (var str in DirSearch(srcRoot))
            {
                string s = str.Replace("\\", "/");

                if (this.sources.TryGetValue(name, out var source))
                {
                    source.Add(s);
                }
                else
                {
                    sources.Add(name, new List<string> { s });
                }
            }
        }

        public static string GetModAssemblyName(string path)
        {
            if (_dirToModNameCache.TryGetValue(path, out var name))
            {
                return name;
            }

            try
            {
                name = FindModNameInternal(path);
                name = ToValidAssemblyIdentifier(name);
            }
            catch (Exception e)
            {
                DebugConsole.AddWarning("Failed to find mod name for " + path + ": " + e.Message);
                return path;
            }

            _dirToModNameCache[path] = name;
            return name;
        }        
        
        private static string FindModNameInternal(string path)
        {
            // Preferred way: find "name" attr value from filelist.xml
            var fileListPath = Path.Combine(path, ContentPackage.FileListFileName);
            if (File.Exists(fileListPath))
            {
                var doc = XDocument.Load(fileListPath);
                var name = doc.XPathEvaluate("string(//contentpackage/@name)") as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _dirToModNameCache.Add(path, name);
                    return name;
                }
            }
            
            // fallback: use the name of the folder
            return Path.GetFileName(path);
        }

        private static string ToValidAssemblyIdentifier(string str)
        {
            // Replace any invalid characters with underscores
            string validStr = new string(str.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

            // If the first character is not a letter or underscore, add an underscore to the beginning
            if (!char.IsLetter(validStr[0]) && validStr[0] != '_')
            {
                validStr = "_" + validStr;
            }

            return validStr;
        }
        
        private void RunFolder(string folder)
        {

            AddSources(folder, Path.Combine(folder, "CSharp/Shared"));

#if SERVER
            AddSources(folder, Path.Combine(folder, "CSharp/Server"));
#else
            AddSources(folder, Path.Combine(folder, "CSharp/Client"));
#endif
        }

        private Dictionary<string, List<SyntaxTree>> ParseSources()
        {
            var result = new Dictionary<string, List<SyntaxTree>>();
            if (sources.Count <= 0) throw new Exception("No Cs sources detected");

            foreach ((var modName, var src) in sources)
            {
                try
                {
                    var syntaxTrees = new List<SyntaxTree> { AssemblyInfoSyntaxTree(modName) };
                    foreach (var file in src)
                    {
                        var tree = SyntaxFactory.ParseSyntaxTree(File.ReadAllText(file), ParseOptions, file);
                        syntaxTrees.Add(tree);
                    }
                    
                    result.Add(modName, syntaxTrees);
                }
                catch (Exception ex)
                {
                    LuaCsLogger.LogError("Error loading '" + modName + "':\n" + ex.Message + "\n" + ex.StackTrace, LuaCsMessageOrigin.CSharpMod);
                }
            }

            return result;
        }

        private ContentPackage FindSourcePackage(Diagnostic diagnostic)
        {
            if (diagnostic.Location.SourceTree == null)
            {
                return null;
            }

            string path = diagnostic.Location.SourceTree.FilePath;
            foreach (var package in ContentPackageManager.AllPackages)
            {
                if (Path.GetFullPath(path).StartsWith(Path.GetFullPath(package.Dir)))
                {
                    return package;
                }
            }

            return null;
        }

        private bool TryCompileMod(string assemblyName, IList<SyntaxTree> syntaxTrees, out Assembly assembly)
        {
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithMetadataImportOptions(MetadataImportOptions.All)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithAllowUnsafe(true);
            
            var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
            topLevelBinderFlagsProperty.SetValue(options, (uint)1 << 22);

            var compilation = CSharpCompilation.Create(assemblyName, syntaxTrees, defaultReferences, options);
            using var mem = new MemoryStream();
            
            var result = compilation.Emit(mem);
            if (!result.Success)
            {
                IEnumerable<Diagnostic> failures = result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error);

                string errStr = $"CS MOD \"{assemblyName}\" NOT LOADED | Compilation errors:";
                foreach (Diagnostic diagnostic in failures)
                {
                    errStr += $"\n{diagnostic}";
#if CLIENT
                    ContentPackage package = FindSourcePackage(diagnostic);
                    if (package != null)
                    {
                        LuaCsLogger.ShowErrorOverlay($"{package.Name} {package.ModVersion} is causing compilation errors. Check debug console for more details.", 7f, 7f);
                    }
#endif
                }
                LuaCsLogger.LogError(errStr, LuaCsMessageOrigin.CSharpMod);
            }
            else
            {
                mem.Seek(0, SeekOrigin.Begin);
                assembly = LoadFromStream(mem);
                return true;
            }

            assembly = null;
            return false;
        }

        public List<Type> CompileAll() 
        {
            var mods = ParseSources();
            var assemblies = CompileAssemblies(mods);
            var acMods = RegisterACsMods(assemblies);
            
            if (!acMods.Any())
            {
                LuaCsLogger.LogError("No Cs mods loaded.", LuaCsMessageOrigin.CSharpMod);
            }

            return acMods;
        }

        private List<Assembly> CompileAssemblies(Dictionary<string, List<SyntaxTree>> parsedMods)
        {
            var assemblies = new List<Assembly>();
            foreach (var (modName, syntaxTrees) in parsedMods)
            {
                if (TryCompileMod(modName, syntaxTrees, out var assembly))
                {
                    assemblies.Add(assembly);
                }
            }
            LoadedAssemblies.AddRange(assemblies);

            return assemblies;
        }

        private List<Type> RegisterACsMods(List<Assembly> assemblies)
        {
            var acMods = new List<Type>();
            foreach (var assembly in assemblies)
            {
                RegisterAssemblyWithNativeGame(assembly);
                try
                {
                    acMods.AddRange(assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(ACsMod))));
                }
                catch (ReflectionTypeLoadException re)
                {
                    LuaCsLogger.LogError($"Unable to load CsMod Types for {assembly.FullName}. {re.Message}", LuaCsMessageOrigin.CSharpMod);
                }
            }

            return acMods;
        }
        
        public Type GetTypeFromLoadedAssemblies(string fullName)
        {
            foreach (var loadedAssembly in this.LoadedAssemblies)
            {
                var type = loadedAssembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
        
        /// <summary>
        /// This function should be used whenever a new assembly is created. Wrapper to allow more complicated setup later if need be.
        /// </summary>
        private static void RegisterAssemblyWithNativeGame(Assembly assembly)
        {
            Barotrauma.ReflectionUtils.AddNonAbstractAssemblyTypes(assembly);
        }

        /// <summary>
        /// This function should be used whenever a new assembly is about to be destroyed/unloaded. Wrapper to allow more complicated setup later if need be.
        /// </summary>
        /// <param name="assembly">Assembly to remove</param>
        private static void UnregisterAssemblyFromNativeGame(Assembly assembly)
        {
            Barotrauma.ReflectionUtils.RemoveAssemblyFromCache(assembly);
        }

        private static string[] DirSearch(string sDir)
        {
            if (!Directory.Exists(sDir))
            {
                return new string[] {};
            }

            return Directory.GetFiles(sDir, "*.cs", SearchOption.AllDirectories);
        }

        public void Clear()
        {
            foreach (var loadedAssembly in LoadedAssemblies)
            {
                UnregisterAssemblyFromNativeGame(loadedAssembly);
            }
            LoadedAssemblies.Clear();
        }
    }
}
