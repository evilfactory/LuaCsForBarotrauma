using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Barotrauma.CharacterEditor;
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Data;

// ReSharper disable ObjectCreationAsStatement

namespace Barotrauma
{
    partial class LuaCsSetup
    {
        private bool _isClientPromptActive;

        /// <summary>
        /// Returns whether execution should continue
        /// </summary>
        public bool CheckReadyToRun()
        {
            // Fast exit if enabled
            if (this.IsCsEnabled)
            {
                return true;
            }
            
            StringBuilder sb = new StringBuilder();

            foreach (ContentPackage cp in PackageManagementService.GetLoadedAssemblyPackages())
            {
                if (cp.NameMatches(PackageId))
                {
                    continue;
                }

                if (cp.UgcId.TryUnwrap(out ContentPackageId id))
                {
                    sb.AppendLine($"- {cp.Name} ({id})");
                }
                else
                {
                    sb.AppendLine($"- {cp.Name} (Not On Workshop)");
                }
            }

            if (string.IsNullOrEmpty(sb.ToString()))
            {
                return true;
            }

            if (!_isClientPromptActive)
            {
                _isClientPromptActive = true;
                if (GameMain.Client == null || GameMain.Client.IsServerOwner)
                {
                    DisplayCsModsPromptServer(sb);
                    return true;
                }
                else
                {
                    DisplayCsModsPromptClient(sb);
                    return false;
                }
            }
            else
            {
                return false;
            }

            void DisplayCsModsPromptServer(StringBuilder sb)
            {
                var msg = new GUIMessageBox("", $"You have CSharp mods enabled but don't have the CSharp Scripting enabled, " +
                                      $"those mods might not work, go to the Main Menu, click on LuaCs Settings and check Enable CSharp Scripting.\n\n{sb}");
                foreach (var button in msg.Buttons)
                {
                    var old = button.OnClicked;
                    button.OnClicked = (btn, obj) =>
                    {
                        var ret = old?.Invoke(btn, obj);
                        _isClientPromptActive = false;
                        return ret ?? true;
                    };
                }
            }

            void DisplayCsModsPromptClient(StringBuilder sb)
            {
                GUIMessageBox msg = new GUIMessageBox(
                    "Confirm",
                    $"This server has the following CSharp mods installed: \n{sb}\nDo you wish to run them? Cs mods are not sandboxed so make sure you trust these mods.",
                    new LocalizedString[2] { "Run", "Don't Run" });

                msg.Buttons[0].OnClicked = (GUIButton button, object obj) =>
                {
                    try
                    { 
                        this._isClientPromptActive = false;
                        CoroutineManager.Invoke(() =>
                        {
                            SetRunState(RunState.LoadedNoExec);
                            this.IsCsEnabled = true;
                            SetRunState(RunState.Running);
                        }, 0f);
                        return true;
                    }
                    finally
                    {
                        msg.Close();
                    }
                };

                msg.Buttons[1].OnClicked = (GUIButton button, object obj) =>
                {
                    try
                    {
                        // avoid a TOCTOU scenario.
                        this.IsCsEnabled = false;
                        this._isClientPromptActive = false;
                        SetRunState(RunState.LoadedNoExec);
                        SetRunState(RunState.Running);
                        return true;
                    }
                    finally
                    {
                        msg.Close();
                    }
                };
            }
        }

        private void SetupServicesProviderClient(IServicesProvider serviceProvider)
        {
            serviceProvider.RegisterServiceType<IUIStylesService, UIStylesService>(ServiceLifetime.Singleton);
            // supplied via factory
            //serviceProvider.RegisterServiceType<IUIStylesCollection, UIStylesCollection>(ServiceLifetime.Transient);
            serviceProvider.RegisterServiceType<IParserServiceAsync<ResourceParserInfo, IStylesResourceInfo>, ModConfigFileParserService>(ServiceLifetime.Transient);
            serviceProvider.RegisterServiceType<IUIStylesCollection.IFactory, UIStylesCollection.Factory>(ServiceLifetime.Transient);
            serviceProvider.RegisterServiceType<ISettingsMenuSystem, SettingsMenuSystem>(ServiceLifetime.Singleton);
        }

        /// <summary>
        /// Handles changes in game states tracked by screen changes.
        /// </summary>
        /// <param name="screen">The new game screen.</param>
        public partial void OnScreenSelected(Screen screen)
        {
            /*Note: This logic needs to be run after the triggering event so that recursion scenarios (ie. resetting the EventService)
             do not occur, so we delay it by one game tick.*/
            CoroutineManager.Invoke(() =>
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
                    case TestScreen: // notes: TestScreen is a Linux edge case editor screen and is deprecated.
                        if (!CheckReadyToRun())
                        {
                            if (CurrentRunState >= RunState.Running)
                            {
                                SetRunState(RunState.LoadedNoExec);
                            }
                            return;
                        }

                        SetRunState(RunState.Running);
                        break;
                    default:
                        Logger.LogError(
                            $"{nameof(LuaCsSetup)}: Received an unknown screen {screen?.GetType().Name ?? "'null screen'"}. Retarding load state to 'unloaded'.");
                        SetRunState(RunState.Unloaded);
                        break;
                }
            }, delay: 0f); // min is one tick delay.
        }
    }
}
