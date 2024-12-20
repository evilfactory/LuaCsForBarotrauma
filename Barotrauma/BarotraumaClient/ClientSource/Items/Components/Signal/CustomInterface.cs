﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Barotrauma.Items.Components
{
    partial class CustomInterface
    {
        private readonly List<GUIComponent> uiElements = new List<GUIComponent>();
        private GUILayoutGroup uiElementContainer;

        private bool suppressNetworkEvents;

        private GUIComponent insufficientPowerWarning;

        private Point ElementMaxSize => new Point(uiElementContainer.Rect.Width, (int)(65 * GUI.yScale));

        public override bool RecreateGUIOnResolutionChange => true;

        partial void InitProjSpecific()
        {
            CreateGUI();
        }

        protected override void CreateGUI()
        {
            uiElements.Clear();
            var visibleElements = customInterfaceElementList.Where(ciElement => !string.IsNullOrEmpty(ciElement.Label));
            uiElementContainer = new GUILayoutGroup(new RectTransform(GuiFrame.Rect.Size - GUIStyle.ItemFrameMargin, GuiFrame.RectTransform, Anchor.Center)
            {
                AbsoluteOffset = GUIStyle.ItemFrameOffset
            },
                childAnchor: customInterfaceElementList.Count > 1 ? Anchor.TopCenter : Anchor.Center)
            {
                RelativeSpacing = 0.05f,
                Stretch = visibleElements.Count() > 2,
            };

            float elementSize = Math.Min(1.0f / visibleElements.Count(), 1);
            foreach (CustomInterfaceElement ciElement in visibleElements)
            {
                if (ciElement.InputType is CustomInterfaceElement.InputTypeOption.Number or CustomInterfaceElement.InputTypeOption.Text)
                {
                    var layoutGroup = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, elementSize), uiElementContainer.RectTransform), isHorizontal: true)
                    {
                        RelativeSpacing = 0.02f,
                        UserData = ciElement
                    };
                    new GUITextBlock(new RectTransform(new Vector2(0.5f, 1.0f), layoutGroup.RectTransform), 
                        TextManager.Get(ciElement.Label).Fallback(ciElement.Label));
                    if (ciElement.InputType is CustomInterfaceElement.InputTypeOption.Text)
                    {
                        var textBox = new GUITextBox(new RectTransform(new Vector2(0.5f, 1.0f), layoutGroup.RectTransform), ciElement.Signal, style: "GUITextBoxNoIcon")
                        {
                            OverflowClip = true,
                            UserData = ciElement,
                            MaxTextLength = ciElement.MaxTextLength
                        };
                        //reset size restrictions set by the Style to make sure the elements can fit the interface
                        textBox.RectTransform.MinSize = textBox.Frame.RectTransform.MinSize = new Point(0, 0);
                        textBox.RectTransform.MaxSize = textBox.Frame.RectTransform.MaxSize = new Point(int.MaxValue, int.MaxValue);
                        textBox.OnDeselected += (tb, key) =>
                        {
                            if (GameMain.Client == null)
                            {
                                TextChanged(tb.UserData as CustomInterfaceElement, textBox.Text);
                            }
                            else
                            {
                                CreateClientEventWithCorrectionDelay();
                            }
                        };

                        textBox.OnEnterPressed += (tb, text) =>
                        {
                            tb.Deselect();
                            return true;
                        };
                        uiElements.Add(textBox);
                    }
                    else
                    {
                        GUINumberInput numberInput = null;
                        if (ciElement.NumberType == NumberType.Float)
                        {
                            TryParseFloatInvariantCulture(ciElement.Signal, out float floatSignal);
                            TryParseFloatInvariantCulture(ciElement.NumberInputMin, out float numberInputMin);
                            TryParseFloatInvariantCulture(ciElement.NumberInputMax, out float numberInputMax);
                            TryParseFloatInvariantCulture(ciElement.NumberInputStep, out float numberInputStep);
                            numberInput = new GUINumberInput(new RectTransform(new Vector2(0.5f, 1.0f), layoutGroup.RectTransform), NumberType.Float)
                            {
                                UserData = ciElement,
                                MinValueFloat = numberInputMin,
                                MaxValueFloat = numberInputMax,
                                FloatValue = Math.Clamp(floatSignal, numberInputMin, numberInputMax),
                                DecimalsToDisplay = ciElement.NumberInputDecimalPlaces,
                                ValueStep = numberInputStep,
                                OnValueChanged = (ni) =>
                                {
                                    ValueChanged(ni.UserData as CustomInterfaceElement, ni.FloatValue);
                                    if (!suppressNetworkEvents && GameMain.Client != null)
                                    {
                                        CreateClientEventWithCorrectionDelay();
                                    }
                                }
                            };
                        }
                        else if (ciElement.NumberType == NumberType.Int)
                        {
                            int.TryParse(ciElement.Signal, out int intSignal);
                            int.TryParse(ciElement.NumberInputMin, out int numberInputMin);
                            int.TryParse(ciElement.NumberInputMax, out int numberInputMax);
                            TryParseFloatInvariantCulture(ciElement.NumberInputStep, out float numberInputStep);
                            numberInput = new GUINumberInput(new RectTransform(new Vector2(0.5f, 1.0f), layoutGroup.RectTransform), NumberType.Int)
                            {
                                UserData = ciElement,
                                MinValueInt = numberInputMin,
                                MaxValueInt = numberInputMax,
                                IntValue = Math.Clamp(intSignal, numberInputMin, numberInputMax),
                                ValueStep = numberInputStep,
                                OnValueChanged = (ni) =>
                                {
                                    ValueChanged(ni.UserData as CustomInterfaceElement, ni.IntValue);                                    
                                    if (!suppressNetworkEvents && GameMain.Client != null)
                                    {
                                        CreateClientEventWithCorrectionDelay();
                                    }
                                }
                            };
                        }
                        else
                        {
                            DebugConsole.LogError($"Error creating a CustomInterface component: unexpected NumberType \"{(ciElement.NumberType.HasValue ? ciElement.NumberType.Value.ToString() : "none")}\"",
                                contentPackage: item.Prefab.ContentPackage);
                        }
                        if (numberInput != null)
                        {
                            //reset size restrictions set by the Style to make sure the elements can fit the interface
                            numberInput.RectTransform.MinSize = numberInput.LayoutGroup.RectTransform.MinSize = new Point(0, 0);
                            numberInput.RectTransform.MaxSize = numberInput.LayoutGroup.RectTransform.MaxSize = new Point(int.MaxValue, int.MaxValue);
                            uiElements.Add(numberInput);
                        }
                    }
                }
                else if (ciElement.InputType is CustomInterfaceElement.InputTypeOption.TickBox)
                {
                    var tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, elementSize), uiElementContainer.RectTransform)
                    {
                        MaxSize = ElementMaxSize
                    }, TextManager.Get(ciElement.Label).Fallback(ciElement.Label))
                    {
                        UserData = ciElement
                    };
                    tickBox.OnSelected += (tBox) =>
                    {
                        TickBoxToggled(tBox.UserData as CustomInterfaceElement, tBox.Selected);                        
                        if (!suppressNetworkEvents && GameMain.Client != null)
                        {
                            CreateClientEventWithCorrectionDelay();
                        }
                        return true;
                    };
                    //reset size restrictions set by the Style to make sure the elements can fit the interface
                    tickBox.RectTransform.MinSize = new Point(0, 0);
                    tickBox.RectTransform.MaxSize = new Point(int.MaxValue, int.MaxValue);
                    uiElements.Add(tickBox);
                }
                else if (ciElement.InputType is CustomInterfaceElement.InputTypeOption.Button)
                {
                    var btn = new GUIButton(new RectTransform(new Vector2(1.0f, elementSize), uiElementContainer.RectTransform),
                        TextManager.Get(ciElement.Label).Fallback(ciElement.Label), style: "DeviceButton")
                    {
                        UserData = ciElement
                    };
                    btn.OnClicked += (_, userdata) =>
                    {
                        CustomInterfaceElement btnElement = userdata as CustomInterfaceElement;
                        if (GameMain.Client == null)
                        {
                            ButtonClicked(btnElement);
                        }
                        else if (!suppressNetworkEvents && GameMain.Client != null)
                        {
                            //don't use CreateClientEventWithCorrectionDelay here, because buttons have no state,
                            //which means we don't need to worry about server updates interfering with client-side changes to the values in the interface
                            item.CreateClientEvent(this, new EventData(btnElement));
                        }
                        return true;
                    };

                    //reset size restrictions set by the Style to make sure the elements can fit the interface
                    btn.RectTransform.MinSize = btn.Frame.RectTransform.MinSize = new Point(0, 0);
                    btn.RectTransform.MaxSize = btn.Frame.RectTransform.MaxSize = ElementMaxSize;

                    uiElements.Add(btn);
                }
            }

            if (ShowInsufficientPowerWarning)
            {
                insufficientPowerWarning = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), GuiFrame.RectTransform, Anchor.BottomCenter, Pivot.TopCenter) { MinSize = new Point(0, GUI.IntScale(30)) },
                    TextManager.Get("SteeringNoPowerTip"), font: GUIStyle.Font, wrap: true, style: "GUIToolTip", textAlignment: Alignment.Center)
                {
                    AutoScaleHorizontal = true,
                    Visible = false
                };
            }

            void CreateClientEventWithCorrectionDelay()
            {
                item.CreateClientEvent(this);
                correctionTimer = CorrectionDelay;
            }
        }

        public override void CreateEditingHUD(SerializableEntityEditor editor)
        {
            base.CreateEditingHUD(editor);

            if (customInterfaceElementList.Count > 0) 
            { 
                PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(customInterfaceElementList[0]);
                PropertyDescriptor labelProperty = properties.Find("Label", false);
                PropertyDescriptor signalProperty = properties.Find("Signal", false);
                for (int i = 0; i < customInterfaceElementList.Count; i++)
                {
                    editor.CreateStringField(customInterfaceElementList[i],
                        new SerializableProperty(labelProperty),
                        customInterfaceElementList[i].Label, "Label #" + (i + 1), "");
                    editor.CreateStringField(customInterfaceElementList[i],
                        new SerializableProperty(signalProperty),
                        customInterfaceElementList[i].Signal, "Signal #" + (i + 1), "");
                }
            }
        }

        public void HighlightElement(int index, Color color, float duration, float pulsateAmount = 0.0f)
        {
            if (index < 0 || index >= uiElements.Count) { return; }
            uiElements[index].Flash(color, duration);

            if (pulsateAmount > 0.0f)
            {
                if (uiElements[index] is GUIButton button)
                {
                    button.Frame.Pulsate(Vector2.One, Vector2.One * (1.0f + pulsateAmount), duration);
                    button.Frame.RectTransform.SetPosition(Anchor.Center);
                }
                else
                {
                    uiElements[index].Pulsate(Vector2.One, Vector2.One * (1.0f + pulsateAmount), duration);
                }
            }
        }

        public override void UpdateHUDComponentSpecific(Character character, float deltaTime, Camera cam)
        {
            bool elementVisibilityChanged = false;
            int visibleElementCount = 0;
            foreach (var uiElement in uiElements)
            {
                if (uiElement.UserData is not CustomInterfaceElement element) { continue; }
                bool visible = Screen.Selected == GameMain.SubEditorScreen || element.StatusEffects.Any() || element.HasPropertyName || (element.Connection != null && element.Connection.Wires.Count > 0);
                if (visible) 
                { 
                    visibleElementCount++; 
                    if (element.GetValueInterval > 0.0f && correctionTimer <= 0.0f)
                    {
                        element.GetValueTimer -= deltaTime;
                        if (element.GetValueTimer <= 0.0f)
                        {
                            SetSignalToPropertyValue(element);
                            UpdateSignalProjSpecific(uiElement);
                            element.GetValueTimer = element.GetValueInterval;
                        }
                    }
                }
                if (uiElement.Visible != visible)
                {
                    uiElement.Visible = visible;
                    uiElement.IgnoreLayoutGroups = !uiElement.Visible;
                    elementVisibilityChanged = true;
                }
            }

            if (elementVisibilityChanged)
            {
                uiElementContainer.Stretch = visibleElementCount > 2;
                uiElementContainer.ChildAnchor = visibleElementCount > 1 ? Anchor.TopCenter : Anchor.Center;
                float elementSize = Math.Min(1.0f / visibleElementCount, 1);
                foreach (var uiElement in uiElements)
                {
                    uiElement.RectTransform.RelativeSize = new Vector2(1.0f, elementSize);
                }
                GuiFrame.Visible = visibleElementCount > 0;
                uiElementContainer.Recalculate();
            }

            if (insufficientPowerWarning != null)
            {
                insufficientPowerWarning.Visible = item.GetComponents<Powered>().Any(p => p.PowerConsumption > 0.0f && p.Voltage < p.MinVoltage);
            }
        }

        partial void UpdateLabelsProjSpecific()
        {
            for (int i = 0; i < labels.Length && i < uiElements.Count; i++)
            {
                if (uiElements[i] is GUIButton button)
                {
                    button.Text = CreateLabelText(i);
                    button.TextBlock.Wrap = button.Text.Contains(' ');
                }
                else if (uiElements[i] is GUITickBox tickBox)
                {
                    tickBox.Text = CreateLabelText(i);
                    tickBox.TextBlock.Wrap = tickBox.Text.Contains(' ');
                }
                else if (uiElements[i] is GUITextBox || uiElements[i] is GUINumberInput)
                {
                    var textBlock = uiElements[i].Parent.GetChild<GUITextBlock>();
                    textBlock.Text = CreateLabelText(i);
                    textBlock.Wrap = textBlock.Text.Contains(' ');
                }
            }

            LocalizedString CreateLabelText(int elementIndex)
            {
                string label = customInterfaceElementList[elementIndex].Label;
                return string.IsNullOrWhiteSpace(label) ?
                    TextManager.GetWithVariable("connection.signaloutx", "[num]", (elementIndex + 1).ToString()) :
                    TextManager.Get(label).Fallback(label);
            }

            uiElementContainer.Recalculate();
            var textBlocks = new List<GUITextBlock>();
            foreach (GUIComponent element in uiElementContainer.Children)
            {
                if (element is GUIButton btn)
                {
                    if (btn.TextBlock.TextSize.Y > btn.Rect.Height - btn.TextBlock.Padding.Y - btn.TextBlock.Padding.W)
                    {
                        btn.RectTransform.RelativeSize = new Vector2(btn.RectTransform.RelativeSize.X, btn.RectTransform.RelativeSize.Y * 1.5f);
                    }
                    textBlocks.Add(btn.TextBlock);
                }
                else if (element is GUITickBox tickBox)
                {
                    textBlocks.Add(tickBox.TextBlock);
                }
                else if (element is GUILayoutGroup)
                {
                    textBlocks.Add(element.GetChild<GUITextBlock>());
                }
            }
            uiElementContainer.Recalculate();
            GUITextBlock.AutoScaleAndNormalize(textBlocks);
        }

        partial void UpdateSignalsProjSpecific()
        {
            if (signals == null) { return; }
            for (int i = 0; i < signals.Length && i < uiElements.Count; i++)
            {
                UpdateSignalProjSpecific(uiElements[i]);
            }
        }

        private void UpdateSignalProjSpecific(GUIComponent uiElement)
        {
            if (uiElement.UserData is not CustomInterfaceElement element) { return; }

            suppressNetworkEvents = true;

            string signal = element.Signal;
            if (uiElement is GUITextBox tb)
            {
                tb.Text = Screen.Selected is { IsEditor: true } ?
                    signal :
                    TextManager.Get(signal).Fallback(signal).Value;
            }
            else if (uiElement is GUINumberInput ni)
            {
                if (ni.InputType == NumberType.Int)
                {
                    if (int.TryParse(signal, out int value))
                    {
                        ni.IntValue = value;
                    }
                    else if (float.TryParse(signal, out float floatValue))
                    {
                        ni.IntValue = (int)MathF.Round(floatValue);
                    }
                }
            }
            else if (uiElement is GUITickBox tickBox)
            {
                tickBox.Selected = signal.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            suppressNetworkEvents = false;
        }

        public void ClientEventWrite(IWriteMessage msg, NetEntityEvent.IData extraData = null)
        {
            //extradata contains an array of buttons clicked by the player (or nothing if the player didn't click anything)
            for (int i = 0; i < customInterfaceElementList.Count; i++)
            {
                var element = customInterfaceElementList[i];
                switch (element.InputType)
                {
                    case CustomInterfaceElement.InputTypeOption.Number:
                        switch (element.NumberType)
                        {
                            case NumberType.Float:
                                msg.WriteString(((GUINumberInput)uiElements[i]).FloatValue.ToString());
                                break;
                            case NumberType.Int:
                            default:
                                msg.WriteString(((GUINumberInput)uiElements[i]).IntValue.ToString());
                                break;
                        }
                        break;
                    case CustomInterfaceElement.InputTypeOption.Text:
                        msg.WriteString(((GUITextBox)uiElements[i]).Text);
                        break;
                    case CustomInterfaceElement.InputTypeOption.TickBox:
                        msg.WriteBoolean(((GUITickBox)uiElements[i]).Selected);
                        break;
                    case CustomInterfaceElement.InputTypeOption.Button:
                        msg.WriteBoolean(extraData is Item.ComponentStateEventData { ComponentData: EventData eventData } && eventData.BtnElement == customInterfaceElementList[i]);
                        break;
                }
            }
        }

        public void ClientEventRead(IReadMessage msg, float sendingTime)
        {
            int msgStartPos = msg.BitPosition;
            suppressNetworkEvents = true;
            try
            {
                string[] stringValues = new string[customInterfaceElementList.Count];
                bool[] boolValues = new bool[customInterfaceElementList.Count];
                for (int i = 0; i < customInterfaceElementList.Count; i++)
                {
                    var element = customInterfaceElementList[i];
                    switch (element.InputType)
                    {
                        case CustomInterfaceElement.InputTypeOption.Number:
                        case CustomInterfaceElement.InputTypeOption.Text:
                            stringValues[i] = msg.ReadString();
                            break;
                        case CustomInterfaceElement.InputTypeOption.TickBox:
                        case CustomInterfaceElement.InputTypeOption.Button:
                            boolValues[i] = msg.ReadBoolean();
                            break;
                    }
                }

                if (correctionTimer > 0.0f)
                {
                    int msgLength = msg.BitPosition - msgStartPos;
                    msg.BitPosition = msgStartPos;
                    StartDelayedCorrection(msg.ExtractBits(msgLength), sendingTime);
                    return;
                }

                for (int i = 0; i < customInterfaceElementList.Count; i++)
                {
                    var element = customInterfaceElementList[i];
                    switch (element.InputType)
                    {
                        case CustomInterfaceElement.InputTypeOption.Number:
                            switch (element.NumberType)
                            {
                                case NumberType.Int when int.TryParse(stringValues[i], out int value):
                                    ValueChanged(element, value);
                                    break;
                                case NumberType.Float when TryParseFloatInvariantCulture(stringValues[i], out float value):
                                    ValueChanged(element, value);
                                    break;
                            }
                            break;
                        case CustomInterfaceElement.InputTypeOption.Text:
                            TextChanged(element, stringValues[i]);
                            break;
                        case CustomInterfaceElement.InputTypeOption.TickBox:
                            bool tickBoxState = boolValues[i];
                            ((GUITickBox)uiElements[i]).Selected = tickBoxState;
                            TickBoxToggled(element, tickBoxState);
                            break;
                        case CustomInterfaceElement.InputTypeOption.Button:
                            if (boolValues[i])
                            {
                                ButtonClicked(element);
                            }
                            break;
                    }
                }

                UpdateSignalsProjSpecific();
            }
            finally
            {
                suppressNetworkEvents = false;
            }
        }
    }
}
