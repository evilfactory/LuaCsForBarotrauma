using Barotrauma.Networking;
using System;
using System.IO;
using System.Linq;

namespace Barotrauma
{
    static partial class LuaCsInstaller
    {
        public static void Install()
        {
            throw new NotImplementedException();
            // TODO: Refactor the installer to not be dependent on LuaCsSetup.
            /*ContentPackage luaPackage = LuaCsSetup.GetPackage();

            if (luaPackage == null)
            {
                GameMain.Server.SendChatMessage("Couldn't find the LuaCs For Barotrauma package.", ChatMessageType.ServerMessageBox);
                return;
            }

            try
            {
                string path = Path.GetDirectoryName(luaPackage.Path);

                string[] filesToCopy = trackingFiles.Concat(Directory.EnumerateFiles(path, "*.dll", SearchOption.AllDirectories)
                    .Where(s => s.Contains("mscordaccore_amd64_amd64")).Select(s => Path.GetFileName(s))).ToArray();

                CreateMissingDirectory();

                File.Move("Barotrauma.dll", "Temp/Original/Barotrauma.dll", true);
                File.Move("Barotrauma.deps.json", "Temp/Original/Barotrauma.deps.json", true);
                File.Move("Barotrauma.pdb", "Temp/Original/Barotrauma.pdb", true);
                File.Move("BarotraumaCore.dll", "Temp/Original/BarotraumaCore.dll", true);
                File.Move("BarotraumaCore.pdb", "Temp/Original/BarotraumaCore.pdb", true);

                File.Move("System.Reflection.Metadata.dll", "Temp/Original/System.Reflection.Metadata.dll", true);
                File.Move("System.Collections.Immutable.dll", "Temp/Original/System.Collections.Immutable.dll", true);
                File.Move("System.Runtime.CompilerServices.Unsafe.dll", "Temp/Original/System.Runtime.CompilerServices.Unsafe.dll", true);

                foreach (string file in filesToCopy)
                {
                    if (File.Exists(file))
                    {
                        File.Move(file, "Temp/ToDelete/" + file, true);
                    }
                    File.Copy(Path.Combine(path, "Binary", file), file, true);
                }

                File.WriteAllText(LuaCsSetup.VersionFile, luaPackage.ModVersion);

#if WINDOWS
                File.WriteAllText("LuaCsDedicatedServer.bat", "\"%LocalAppData%/Daedalic Entertainment GmbH/Barotrauma/WorkshopMods/Installed/2559634234/Binary/DedicatedServer.exe\"");
#endif
            }
            catch (UnauthorizedAccessException e)
            {
                LuaCsLogger.LogError($"Unauthorized file access exception. This usually means you already have LuaCs installed. ${e}", LuaCsMessageOrigin.LuaCs);

                return;
            }
            catch (Exception e)
            {
                LuaCsLogger.HandleException(e, LuaCsMessageOrigin.LuaCs);

                return;
            }

            GameMain.Server.SendChatMessage("Client-Side LuaCs installed, restart your game to apply changes.", ChatMessageType.ServerMessageBox);*/
        }
    }
}
