using System;
using System.IO;
using System.Linq;

namespace Barotrauma
{
    static partial class LuaCsInstaller
    {
        public static void Uninstall()
        {
            if (!File.Exists("Temp/Original/Barotrauma.dll"))
            {
                new GUIMessageBox("Error", "Error: Temp/Original/Barotrauma.dll not found, Github version? Use Steam validate files instead.");

                return;
            }

            var msg = new GUIMessageBox("Confirm", "Are you sure you want to remove Client-Side LuaCs?", new LocalizedString[2] { TextManager.Get("Yes"), TextManager.Get("Cancel") });

            msg.Buttons[0].OnClicked = (GUIButton button, object obj) =>
            {
                msg.Close();

                string[] filesToRemove = new string[]
                {
                    "Barotrauma.dll", "Barotrauma.deps.json", "Barotrauma.pdb", "BarotraumaCore.dll", "BarotraumaCore.pdb",
                    "System.Reflection.Metadata.dll", "System.Collections.Immutable.dll",
                    "System.Runtime.CompilerServices.Unsafe.dll"
                };
                try
                {
                    CreateMissingDirectory();

                    foreach (string file in filesToRemove)
                    {
                        File.Move(file, "Temp/ToDelete/" + file, true);
                        File.Move("Temp/Original/" + file, file, true);
                    }
                }
                catch (Exception e)
                {
                    new GUIMessageBox("Error", $"{e} {e.InnerException} \nTry verifying files instead.");
                    return false;
                }

                new GUIMessageBox("Restart", "Restart your game to apply the changes. If the mod continues to stay active after the restart, try verifying games instead.");

                return true;
            };

            msg.Buttons[1].OnClicked = (GUIButton button, object obj) =>
            {
                msg.Close();
                return true;
            };
        }
    }
}
