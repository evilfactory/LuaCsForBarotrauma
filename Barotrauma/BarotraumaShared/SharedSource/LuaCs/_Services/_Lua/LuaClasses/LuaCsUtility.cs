using Barotrauma.Items.Components;
using Barotrauma.Networking;
using MoonSharp.Interpreter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml.Linq;
using Barotrauma.LuaCs;

namespace Barotrauma
{
    partial class LuaCsFile
    {
        public static bool CanReadFromPath(string path)
        {
            string getFullPath(string p) => System.IO.Path.GetFullPath(p).CleanUpPath();

            path = getFullPath(path);

            bool pathStartsWith(string prefix) => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            string localModsDir = getFullPath(ContentPackage.LocalModsDir);
            string workshopModsDir = getFullPath(ContentPackage.WorkshopModsDir);
#if CLIENT
            string tempDownloadDir = getFullPath(ModReceiver.DownloadFolder);
#endif
            if (pathStartsWith(getFullPath(string.IsNullOrEmpty(GameSettings.CurrentConfig.SavePath) ? SaveUtil.DefaultSaveFolder : GameSettings.CurrentConfig.SavePath)))
                return true;

            if (pathStartsWith(localModsDir))
                return true;

            if (pathStartsWith(workshopModsDir))
                return true;

#if CLIENT
            if (pathStartsWith(tempDownloadDir))
                return true;
#endif

            if (pathStartsWith(getFullPath(".")))
                return true;

            return false;
        }

        public static bool CanWriteToPath(string path)
        {
            string getFullPath(string p) => System.IO.Path.GetFullPath(p).CleanUpPath();

            path = getFullPath(path);

            bool pathStartsWith(string prefix) => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            foreach (var package in ContentPackageManager.AllPackages)
            {
                if (package.UgcId.ValueEquals(new SteamWorkshopId(GameMain.LuaCs.LuaForBarotraumaSteamId)) 
                    && pathStartsWith(getFullPath(package.Path)))
                {
                    return false;
                }
            }

            if (pathStartsWith(getFullPath(string.IsNullOrEmpty(GameSettings.CurrentConfig.SavePath) ? SaveUtil.DefaultSaveFolder : GameSettings.CurrentConfig.SavePath)))
                return true;

            if (pathStartsWith(getFullPath(ContentPackage.LocalModsDir)))
                return true;

            if (pathStartsWith(getFullPath(ContentPackage.WorkshopModsDir)))
                return true;
#if CLIENT
            if (pathStartsWith(getFullPath(ModReceiver.DownloadFolder)))
                return true;
#endif

            return false;
        }

        public static bool IsPathAllowedException(string path, bool write = true, LuaCsMessageOrigin origin = LuaCsMessageOrigin.Unknown)
        {
            if (write)
            {
                if (CanWriteToPath(path))
                {
                    return true;
                }
                else
                {
                    throw new Exception("File access to \"" + path + "\" not allowed.");
                }
            }
            else
            {
                if (CanReadFromPath(path))
                {
                    return true;
                }
                else
                {
                    throw new Exception("File access to \"" + path + "\" not allowed.");
                }
            }
        }

        public static bool IsPathAllowedLuaException(string path, bool write = true) =>
            IsPathAllowedException(path, write, LuaCsMessageOrigin.LuaMod);
        public static bool IsPathAllowedCsException(string path, bool write = true) =>
            IsPathAllowedException(path, write, LuaCsMessageOrigin.CSharpMod);

        public static string Read(string path)
        {
            if (!IsPathAllowedException(path, false))
                return "";

            return File.ReadAllText(path);
        }

        public static void Write(string path, string text)
        {
            if (!IsPathAllowedException(path))
                return;

            File.WriteAllText(path, text);
        }

        public static void Delete(string path)
        {
            if (!IsPathAllowedException(path))
                return;

            File.Delete(path);
        }

        public static void DeleteDirectory(string path)
        {
            if (!IsPathAllowedException(path))
                return;

            Directory.Delete(path, true);
        }

        public static void Move(string path, string destination)
        {
            if (!IsPathAllowedException(path))
                return;

            if (!IsPathAllowedException(destination))
                return;

            File.Move(path, destination, true);
        }

        public static FileStream OpenRead(string path)
        {
            if (!IsPathAllowedException(path))
                return null;

            return File.Open(path, FileMode.Open, FileAccess.Read);
        }
        public static FileStream OpenWrite(string path)
        {
            if (!IsPathAllowedException(path))
                return null;

            if (File.Exists(path)) return File.Open(path, FileMode.Truncate, FileAccess.Write);
            else return File.Open(path, FileMode.Create, FileAccess.Write);
        }

        public static bool Exists(string path)
        {
            if (!IsPathAllowedException(path, false))
                return false;

            return File.Exists(path);
        }

        public static bool CreateDirectory(string path)
        {
            if (!IsPathAllowedException(path))
                return false;

            Directory.CreateDirectory(path);

            return true;
        }

        public static bool DirectoryExists(string path)
        {
            if (!IsPathAllowedException(path, false))
                return false;

            return Directory.Exists(path);
        }

        public static string[] GetFiles(string path)
        {
            if (!IsPathAllowedException(path, false))
                return null;

            return Directory.GetFiles(path);
        }

        public static string[] GetDirectories(string path)
        {
            if (!IsPathAllowedException(path, false))
                return new string[] { };

            return Directory.GetDirectories(path);
        }

        public static string[] DirSearch(string sDir)
        {
            if (!IsPathAllowedException(sDir, false))
                return new string[] { };

            List<string> files = new List<string>();

            try
            {
                foreach (string f in Directory.GetFiles(sDir))
                {
                    files.Add(f);
                }

                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        files.Add(f);
                    }
                    DirSearch(d);
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }

            return files.ToArray();
        }
    }
}
