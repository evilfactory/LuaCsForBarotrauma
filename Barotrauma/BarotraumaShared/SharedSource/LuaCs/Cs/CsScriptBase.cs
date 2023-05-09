using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis.Scripting;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Runtime.Loader;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Text;
using System.Runtime.CompilerServices;

namespace Barotrauma
{
    class CsScriptBase : AssemblyLoadContext
    {
        public static Dictionary<string, int> Revision = new();
        
        public CSharpParseOptions ParseOptions { get; protected set; }

        public CsScriptBase() : base(isCollectible: true) {
            ParseOptions = CSharpParseOptions.Default
                .WithPreprocessorSymbols(new[] { LuaCsSetup.IsServer ? "SERVER" : (LuaCsSetup.IsClient ? "CLIENT" : "UNDEFINED") });
        }

        public static SyntaxTree AssemblyInfoSyntaxTree(string asmName)
        {
            Revision[asmName] = Revision.GetValueOrDefault(asmName) + 1;
            var asmInfo = new StringBuilder();
            asmInfo.AppendLine("using System.Reflection;");
            asmInfo.AppendLine($"[assembly: AssemblyMetadata(\"Revision\", \"{Revision[asmName]}\")]");
            asmInfo.AppendLine($"[assembly: AssemblyVersion(\"0.0.0.{Revision[asmName]}\")]");
            return CSharpSyntaxTree.ParseText(asmInfo.ToString(), CSharpParseOptions.Default);
        }

        ~CsScriptBase() { }
    }
}
