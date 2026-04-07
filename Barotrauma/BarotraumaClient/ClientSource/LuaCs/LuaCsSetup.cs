using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Barotrauma.CharacterEditor;
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Data;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;

// ReSharper disable ObjectCreationAsStatement

namespace Barotrauma
{
    partial class LuaCsSetup
    {
        private bool _isClientPromptActive;
        private bool _isCsEnabledForSession = false;
        
        public void CheckRunConditionalHostingCsEnabled(Action onReadyToRun)
        {
            var res = ReadyToRunNoPrompt();
            if (res.ShouldRun)
            {
                onReadyToRun?.Invoke();
                return;
            }
            
            DisplayCsModsPromptClient(res.Item2, (selectedYes) =>
            {
                if (selectedYes)
                {
                    onReadyToRun?.Invoke();
                }
            });
        }

        private (bool ShouldRun, ImmutableArray<ContentPackage> PromptPackages) ReadyToRunNoPrompt()
        {            
            if (this.IsCsEnabled)
            {
                return (true, ImmutableArray<ContentPackage>.Empty);
            }

            if (!ShouldPromptForCs)
            {
                return (true, ImmutableArray<ContentPackage>.Empty);
            }

            ImmutableArray<ContentPackage> contentPackages = PackageManagementService.GetLoadedAssemblyPackages()
                .Where(p => p.Name != PackageId)
                .ToImmutableArray();

            return (contentPackages.IsEmpty, contentPackages);
        }

        partial void CheckReadyToRun(Action onReadyToRun)
        {
            var res = ReadyToRunNoPrompt();
            if (res.ShouldRun)
            {
                onReadyToRun?.Invoke();
                return;
            }
            
            if (GameMain.Client?.ClientPeer is P2POwnerPeer)
            {
                SetCsPolicyAndContinue(true);
                return;
            }

            DisplayCsModsPromptClient(res.PromptPackages, (selectedYes) =>
            {
                SetCsPolicyAndContinue(selectedYes);
                return;
            });

            void SetCsPolicyAndContinue(bool csSessionExecutionPolicy)
            {
                var prevRunState = this.CurrentRunState;
                if (CurrentRunState >= RunState.Running)
                {
                    SetRunState(RunState.LoadedNoExec);
                }
                this._isCsEnabledForSession = csSessionExecutionPolicy;
                CoroutineManager.Invoke(() =>
                {
                    if (CurrentRunState != prevRunState)
                    {
                        SetRunState(prevRunState);
                    }
                    onReadyToRun?.Invoke();
                }, 0f);
            }
        }
        
        void DisplayCsModsPromptClient(ImmutableArray<ContentPackage> contentPackages, Action<bool> onSelection)
        {
            if (_isClientPromptActive) { return; }

            _isClientPromptActive = true;

            GUIMessageBox messageBox = new GUIMessageBox(
                TextManager.Get("warning"),
                relativeSize: new Vector2(0.3f, 0.55f),
                minSize: new Point(400, 500),
                text: string.Empty,
                buttons: []);

            GUILayoutGroup msgBoxLayout = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.75f), messageBox.Content.RectTransform), isHorizontal: false, childAnchor: Anchor.TopCenter)
            {
                RelativeSpacing = 0.01f,
                Stretch = true
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), msgBoxLayout.RectTransform), "The following mods contain CSharp code",
                font: GUIStyle.SubHeadingFont, wrap: true, textAlignment: Alignment.Center);

            GUIListBox packageListBox = new GUIListBox(new RectTransform(new Vector2(1.0f, 0.4f), msgBoxLayout.RectTransform))
            {
                CurrentSelectMode = GUIListBox.SelectMode.None
            };

            foreach (ContentPackage package in contentPackages)
            {
                GUIFrame packageFrame = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.15f), packageListBox.Content.RectTransform), style: "ListBoxElement");
                new GUITextBlock(new RectTransform(new Vector2(1f, 1f), packageFrame.RectTransform), package.Name);
            }

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0f), msgBoxLayout.RectTransform), "CSharp mods are not sandboxed, meaning that they have unrestrictive access to your computer, please make sure you trust these mods before you continue. If you are not hosting a server, selecting cancel will only run Lua-mods.", wrap: true)
            {
                Wrap = true
            };

            GUILayoutGroup buttonLayout = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.25f), messageBox.Content.RectTransform, Anchor.BottomCenter), isHorizontal: false, childAnchor: Anchor.TopCenter);

            new GUIButton(new RectTransform(new Vector2(0.8f, 0.0f), buttonLayout.RectTransform), "Continue")
            {
                TextBlock = { AutoScaleHorizontal = true },
                OnClicked = (btn, userdata) =>
                {
                    _isClientPromptActive = false;
                    onSelection(true);
                    messageBox.Close();
                    return true;
                }
            };

            new GUIButton(new RectTransform(new Vector2(0.8f, 0.0f), buttonLayout.RectTransform), "Cancel")
            {
                OnClicked = (btn, userdata) =>
                {
                    _isClientPromptActive = false;
                    onSelection(false);
                    messageBox.Close();
                    return true;
                }
            };
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
                        CheckReadyToRun(() =>
                        {
                            SetRunState(RunState.Running);
                        });
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
