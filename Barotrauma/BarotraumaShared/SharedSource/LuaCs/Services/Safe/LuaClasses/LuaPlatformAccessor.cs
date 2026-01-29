using MoonSharp.Interpreter.Platforms;
using MoonSharp.Interpreter;
using System;
using System.IO;
using System.Text;

namespace Barotrauma
{
    public class LuaPlatformAccessor : PlatformAccessorBase
    {
        public static FileMode ParseFileMode(string mode)
        {
            mode = mode.Replace("b", "");

            if (mode == "r")
                return FileMode.Open;
            else if (mode == "r+")
                return FileMode.OpenOrCreate;
            else if (mode == "w")
                return FileMode.Create;
            else if (mode == "w+")
                return FileMode.Truncate;
            else
                return FileMode.Append;
        }

        public static FileAccess ParseFileAccess(string mode)
        {
            mode = mode.Replace("b", "");

            if (mode == "r")
                return FileAccess.Read;
            else if (mode == "r+")
                return FileAccess.ReadWrite;
            else if (mode == "w")
                return FileAccess.ReadWrite;
            else if (mode == "w+")
                return FileAccess.ReadWrite;
            else
                return FileAccess.Write;
        }

        public override string GetEnvironmentVariable(string envvarname)
        {
            return null;
        }

        public override CoreModules FilterSupportedCoreModules(CoreModules module)
        {
            return module;
        }

        public override Stream IO_OpenFile(Script script, string filename, Encoding encoding, string mode)
        {
            if (!LuaCsFile.IsPathAllowedLuaException(filename)) { return Stream.Null; }

            FileStream stream = new FileStream(filename, ParseFileMode(mode), ParseFileAccess(mode), FileShare.ReadWrite | FileShare.Delete);
            return stream;
        }

        public override Stream IO_GetStandardStream(StandardFileType type)
        {
            switch (type)
            {
                case StandardFileType.StdIn:
                    return Console.OpenStandardInput();
                case StandardFileType.StdOut:
                    return Console.OpenStandardOutput();
                case StandardFileType.StdErr:
                    return Console.OpenStandardError();
                default:
                    throw new ArgumentException("type");
            }
        }

        public override string IO_OS_GetTempFilename()
        {
            return "LocalMods/temp.txt";
        }

        public override void OS_ExitFast(int exitCode)
        {
            throw new ScriptRuntimeException("usage of os.exit is not allowed.");
        }

        public override bool OS_FileExists(string file)
        {
            return LuaCsFile.Exists(file);
        }

        public override void OS_FileDelete(string file)
        {
            LuaCsFile.Delete(file);
        }

        public override void OS_FileMove(string src, string dst)
        {
            LuaCsFile.Move(src, dst);
        }

        public override int OS_Execute(string cmdline)
        {
            throw new ScriptRuntimeException("usage of os.execute is not allowed.");
        }

        public override string GetPlatformNamePrefix()
        {
            return "lua";
        }

        public override void DefaultPrint(string content)
        {
            System.Diagnostics.Debug.WriteLine(content);
        }
    }
}
