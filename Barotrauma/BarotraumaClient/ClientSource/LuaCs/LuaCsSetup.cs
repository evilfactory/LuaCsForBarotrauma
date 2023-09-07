using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Barotrauma
{
    partial class LuaCsSetup
    {
        public void AddToGUIUpdateList()
        {
            if (!GameMain.LuaCs.Config.DisableErrorGUIOverlay)
            {
                LuaCsLogger.AddToGUIUpdateList();
            }
        }

        public void CheckInitialize()
        {
            List<ContentPackage> csharpMods = new List<ContentPackage>();
            foreach (ContentPackage cp in ContentPackageManager.EnabledPackages.All)
            {
                if (Directory.Exists(cp.Dir + "/CSharp"))
                {
                    csharpMods.Add(cp);
                }
            }

            if (csharpMods.Count == 0 || ShouldRunCs || GameMain.Client == null)
            {
                Initialize();
                return;
            }

            if (GameMain.Client.IsServerOwner)
            {
                new GUIMessageBox("", "You have CSharp mods enabled but don't have the Cs For Barotrauma package enabled, those mods might not work.");
                Initialize();
                return;
            }

            StringBuilder sb = new StringBuilder();

            foreach (ContentPackage cp in csharpMods)
            {
                if (cp.UgcId.TryUnwrap(out ContentPackageId id))
                {
                    sb.AppendLine($"- {cp.Name} ({id})");
                }
                else
                {
                    sb.AppendLine($"- {cp.Name} (Not On Workshop)");
                }
            }

            GUIMessageBox msg = new GUIMessageBox(
                "Confirm",
                $"This server has the following CSharp mods installed: \n{sb}\nDo you wish to run them? Cs mods are not sandboxed so make sure you trust these mods.",
                new LocalizedString[2] { "Run", "Don't Run" });

            msg.Buttons[0].OnClicked = (GUIButton button, object obj) =>
            {
                Initialize(true);
                msg.Close();
                return true;
            };

            msg.Buttons[1].OnClicked = (GUIButton button, object obj) =>
            {
                Initialize();
                msg.Close();
                return true;
            };
        }
    }
}
