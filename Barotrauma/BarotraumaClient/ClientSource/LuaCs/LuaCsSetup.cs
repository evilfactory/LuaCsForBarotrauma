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
            if (!DisableErrorGUIOverlay)
            {
                LuaCsLogger.AddToGUIUpdateList();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns whether the IsCsEnabled has been changed to true/enabled. Returns false if already enabled.</returns>
        public bool CheckCsEnabled()
        {
            // fast exit if enabled or unavailable.
            if (this.IsCsEnabled)
            {
                return false;
            }
            
            bool isCsValueChanged = false;
            
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
                return false;
            }

            GUIMessageBox msg = new GUIMessageBox(
                "Confirm",
                $"This server has the following CSharp mods installed: \n{sb}\nDo you wish to run them? Cs mods are not sandboxed so make sure you trust these mods.",
                new LocalizedString[2] { "Run", "Don't Run" });

            msg.Buttons[0].OnClicked = (GUIButton button, object obj) =>
            {
                this.IsCsEnabled = true;
                isCsValueChanged = true;
                return true;
            };

            msg.Buttons[1].OnClicked = (GUIButton button, object obj) =>
            {
                this.IsCsEnabled = false;
                return true;
            };

            return isCsValueChanged;
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
                    SetRunState(RunState.Unloaded);
                    SetRunState(RunState.LoadedNoExec);
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
                    if (CheckCsEnabled() && this.CurrentRunState >= RunState.Running)
                    {
                        SetRunState(RunState.LoadedNoExec);
                    }
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
