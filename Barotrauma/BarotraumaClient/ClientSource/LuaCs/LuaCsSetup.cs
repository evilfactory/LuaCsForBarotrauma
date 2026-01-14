using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Barotrauma.CharacterEditor;
using Barotrauma.LuaCs.Services;

// ReSharper disable ObjectCreationAsStatement

namespace Barotrauma
{
    partial class LuaCsSetup
    {
        public void AddToGUIUpdateList()
        {
            if (!DisableErrorGUIOverlay.Value)
            {
                LuaCsLogger.AddToGUIUpdateList();
            }
        }

        private partial bool ShouldRunCs() => IsCsEnabled.Value;

        public void CheckCsEnabled()
        {
            var csharpMods = PackageManagementService.GetLoadedAssemblyPackages();

            StringBuilder sb = new StringBuilder();

            foreach (ContentPackage cp in csharpMods)
            {
                if (cp.UgcId.TryUnwrap(out ContentPackageId id))
                    sb.AppendLine($"- {cp.Name} ({id})");
                else
                    sb.AppendLine($"- {cp.Name} (Not On Workshop)");
            }

            if (GameMain.Client == null || GameMain.Client.IsServerOwner)
            {
                new GUIMessageBox("", $"You have CSharp mods enabled but don't have the CSharp Scripting enabled, those mods might not work, go to the Main Menu, click on LuaCs Settings and check Enable CSharp Scripting.\n\n{sb}");
                return;
            }

            GUIMessageBox msg = new GUIMessageBox(
                "Confirm",
                $"This server has the following CSharp mods installed: \n{sb}\nDo you wish to run them? Cs mods are not sandboxed so make sure you trust these mods.",
                new LocalizedString[2] { "Run", "Don't Run" });

            msg.Buttons[0].OnClicked = (GUIButton button, object obj) =>
            {
                this.IsCsEnabled.TrySetValue(true);
                return true;
            };

            msg.Buttons[1].OnClicked = (GUIButton button, object obj) =>
            {
                this.IsCsEnabled.TrySetValue(false);
                return true;
            };
        }

        /// <summary>
        /// Handles changes in game states tracked by screen changes.
        /// </summary>
        /// <param name="screen">The new game screen.</param>
        public partial void OnScreenSelected(Screen screen)
        {
            switch (screen)
            {
                // menus and navigation states
                case MainMenuScreen:
                case ModDownloadScreen: 
                case ServerListScreen: 
                    SetRunState(RunState.Configuration);
                    break;
                 // running lobby or editor states
                case CampaignEndScreen:    
                case CharacterEditorScreen:
                case EventEditorScreen:
                case GameScreen:
                case LevelEditorScreen:
                case NetLobbyScreen:
                case ParticleEditorScreen:
                case RoundSummaryScreen:
                case SpriteEditorScreen:
                case SubEditorScreen: 
                case TestScreen:        // notes: TestScreen is a Linux edge case editor screen and is deprecated.
                    CheckCsEnabled();
                    SetRunState(RunState.Running);
                    break;
                default:
                    Logger.LogError($"{nameof(LuaCsSetup)}: Received an unknown screen {screen?.GetType().Name ?? "'null screen'"}. Retarding load state to 'unloaded'.");
                    SetRunState(RunState.Unloaded);
                    break;
            }
        }
    }
}
