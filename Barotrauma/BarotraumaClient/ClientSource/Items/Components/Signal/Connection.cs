﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma.Items.Components
{
    partial class Connection
    {
        //private static Texture2D panelTexture;
        private static Sprite connector;
        private static Sprite wireVertical;
        private static Sprite connectionSprite;
        private static Sprite connectionSpriteHighlight;
        private static List<Sprite> screwSprites;

        private Color flashColor;
        private float flashDuration = 1.5f;
        
        public float FlashTimer { get; private set; }
        public static Wire DraggingConnected { get; private set; }

        private static float ConnectionSpriteSize => 35.0f * GUI.Scale;

        public static void DrawConnections(SpriteBatch spriteBatch, ConnectionPanel panel, Rectangle dragArea, Character character, 
            out (Vector2 tooltipPos, LocalizedString text) tooltip)
        {
            if (DraggingConnected?.Item?.Removed ?? true)
            {
                DraggingConnected = null;
            }

            Rectangle panelRect = panel.GuiFrame.Rect;
            int x = panelRect.X, y = panelRect.Y;
            int width = panelRect.Width, height = panelRect.Height;

            bool mouseInRect = panelRect.Contains(PlayerInput.MousePosition);

            int totalWireCount = 0;
            foreach (Connection c in panel.Connections)
            {
                totalWireCount += c.Wires.Count;
            }

            Wire equippedWire = null;
            
            bool allowRewiring = GameMain.NetworkMember?.ServerSettings == null || GameMain.NetworkMember.ServerSettings.AllowRewiring || panel.AlwaysAllowRewiring;
            if (allowRewiring && (!panel.Locked && !panel.TemporarilyLocked || Screen.Selected == GameMain.SubEditorScreen))
            {
                //if the Character using the panel has a wire item equipped
                //and the wire hasn't been connected yet, draw it on the panel
                foreach (Item item in character.HeldItems)
                {
                    Wire wireComponent = item.GetComponent<Wire>();
                    if (wireComponent != null)
                    {
                        equippedWire = wireComponent;
                        var connectedEnd = equippedWire.OtherConnection(null);
                        if (connectedEnd?.Item.Submarine != null && panel.Item.Submarine != connectedEnd.Item.Submarine)
                        {
                            equippedWire = null;
                        }
                    }
                }
            }

            tooltip = (Vector2.Zero, string.Empty);

            //two passes: first the connector, then the wires to get the wires to render in front
            for (int i = 0; i < 2; i++)
            {
                Vector2 rightPos = GetRightPos(x, y, width);
                Vector2 leftPos = GetLeftPos(x, y);

                Vector2 rightWirePos = new Vector2(x + width - 5 * GUI.Scale, y + 30 * GUI.Scale);
                Vector2 leftWirePos = new Vector2(x + 5 * GUI.Scale, y + 30 * GUI.Scale);

                int wireInterval = (height - (int)(20 * GUI.Scale)) / Math.Max(totalWireCount, 1);
                int connectorIntervalLeft = GetConnectorIntervalLeft(height, panel);
                int connectorIntervalRight = GetConnectorIntervalRight(height, panel);

                foreach (Connection c in panel.Connections)
                {
                    //if dragging a wire, let the Inventory know so that the wire can be
                    //dropped or dragged from the panel to the players inventory
                    if (DraggingConnected != null && i == 1)
                    {
                        //the wire can only be dragged out if it's not connected to anything at the other end
                        if (Screen.Selected == GameMain.SubEditorScreen ||
                            (DraggingConnected.Connections[0] == null && DraggingConnected.Connections[1] == null) ||
                            (DraggingConnected.Connections.Contains(c) && DraggingConnected.Connections.Contains(null)))
                        {
                            var linkedWire = c.FindWireByItem(DraggingConnected.Item);
                            if (linkedWire != null || panel.DisconnectedWires.Contains(DraggingConnected))
                            {
                                Inventory.DraggingItems.Clear();
                                Inventory.DraggingItems.Add(DraggingConnected.Item);
                            }
                        }
                    }

                    Vector2 position = c.IsOutput ? rightPos : leftPos;
                    if (ConnectionPanel.ShouldDebugDrawWiring)
                    {
                        DrawConnectionDebugInfo(spriteBatch, c, position, GUI.Scale, out var tooltipText);

                        if (!tooltipText.IsNullOrEmpty())
                        {
                            bool mouseOn = Vector2.DistanceSquared(position, PlayerInput.MousePosition) < MathUtils.Pow2(35 * GUI.Scale);
                            if (mouseOn)
                            {
                                tooltip = (position, tooltipText);
                            }
                        }
                    }

                    //outputs are drawn at the right side of the panel, inputs at the left
                    if (c.IsOutput)
                    {
                        if (i == 0)
                        {
                            c.DrawConnection(spriteBatch, panel, rightPos, GetOutputLabelPosition(rightPos, panel, c));
                        }
                        else
                        {
                            c.DrawWires(spriteBatch, panel, rightPos, rightWirePos, mouseInRect, equippedWire, wireInterval);
                        }
                        rightPos.Y += connectorIntervalLeft;
                        rightWirePos.Y += c.Wires.Count * wireInterval;
                    }
                    else
                    {
                        if (i == 0)
                        {
                            c.DrawConnection(spriteBatch, panel, leftPos, GetInputLabelPosition(leftPos, panel, c));
                        }
                        else
                        {
                            c.DrawWires(spriteBatch, panel, leftPos, leftWirePos, mouseInRect, equippedWire, wireInterval);
                        }
                        leftPos.Y += connectorIntervalRight;
                        leftWirePos.Y += c.Wires.Count * wireInterval;
                    }
                }
            }

            if (DraggingConnected != null)
            {
                if (mouseInRect)
                {
                    Vector2 wireDragPos = new Vector2(
                        MathHelper.Clamp(PlayerInput.MousePosition.X, dragArea.X, dragArea.Right),
                        MathHelper.Clamp(PlayerInput.MousePosition.Y, dragArea.Y, dragArea.Bottom));
                    DrawWire(spriteBatch, DraggingConnected, wireDragPos, new Vector2(x + width / 2, y + height - 10), null, panel, "");
                }
                panel.TriggerRewiringSound();

                if (!PlayerInput.PrimaryMouseButtonHeld())
                {
                    if (GameMain.NetworkMember != null || panel.CheckCharacterSuccess(character))
                    {
                        if (DraggingConnected.Connections[0]?.ConnectionPanel == panel ||
                            DraggingConnected.Connections[1]?.ConnectionPanel == panel)
                        {
                            DraggingConnected.RemoveConnection(panel.Item);
                            if (DraggingConnected.Item.ParentInventory == null)
                            {
                                panel.DisconnectedWires.Add(DraggingConnected);
                            }
                            else if (DraggingConnected.Connections[0] == null && DraggingConnected.Connections[1] == null)
                            {
                                DraggingConnected.ClearConnections(user: Character.Controlled);
                            }
                        }
                    }

                    if (GameMain.Client != null)
                    {
                        panel.Item.CreateClientEvent(panel);
                    }

                    DraggingConnected = null;
                }
            }

            //if the Character using the panel has a wire item equipped
            //and the wire hasn't been connected yet, draw it on the panel
            if (equippedWire != null && (DraggingConnected != equippedWire || !mouseInRect))
            {
                if (panel.Connections.Find(c => c.Wires.Contains(equippedWire)) == null)
                {
                    DrawWire(spriteBatch, equippedWire, new Vector2(x + width / 2, y + height - 150 * GUI.Scale),
                        new Vector2(x + width / 2, y + height),
                        null, panel, "");

                    if (DraggingConnected == equippedWire) 
                    {
                        Inventory.DraggingItems.Clear();
                        Inventory.DraggingItems.Add(equippedWire.Item);
                    }
                }
            }


            float step = (width * 0.75f) / panel.DisconnectedWires.Count;
            x = (int)(x + width / 2 - step * (panel.DisconnectedWires.Count - 1) / 2);
            foreach (Wire wire in panel.DisconnectedWires)
            {
                if (wire == DraggingConnected && mouseInRect) { continue; }
                if (wire.HiddenInGame && Screen.Selected == GameMain.GameScreen) { continue; }

                Connection recipient = wire.OtherConnection(null);
                LocalizedString label = recipient == null ? "" : recipient.item.Name + $" ({recipient.DisplayName})";
                if (wire.Locked) { label += "\n" + TextManager.Get("ConnectionLocked"); }
                DrawWire(spriteBatch, wire, new Vector2(x, y + height - 100 * GUI.Scale),
                    new Vector2(x, y + height),
                    null, panel, label);
                x += (int)step;
            }

            //stop dragging a wire item if the cursor is within any connection panel
            //(so we don't drop the item when dropping the wire on a connection)
            if (mouseInRect || (GUI.MouseOn?.UserData is ConnectionPanel && GUI.MouseOn.MouseRect.Contains(PlayerInput.MousePosition))) 
            {
                Inventory.DraggingItems.Clear();
            }
        }

        public static void DrawConnectionDebugInfo(SpriteBatch spriteBatch, Connection c, Vector2 position, float scale, out LocalizedString tooltip)
        {
            Color highlightColor = Color.Transparent;
            if (c.IsPower)
            {
                highlightColor = VisualizeSignal(0.0f, highlightColor, Color.Red);
            }
            else
            {
                highlightColor = VisualizeSignal(c.LastReceivedSignal.TimeSinceCreated, highlightColor, Color.LightGreen);
                highlightColor = VisualizeSignal(c.LastSentSignal.TimeSinceCreated, highlightColor, Color.Orange);
            }

            LocalizedString toolTipText = c.GetToolTip();
            if (!toolTipText.IsNullOrEmpty())
            {
                var glowSprite = GUIStyle.UIGlowCircular.Value.Sprite;
                glowSprite.Draw(spriteBatch, position, highlightColor, glowSprite.size / 2,
                    scale: 45.0f / glowSprite.size.X * scale);
            }

            tooltip = toolTipText;

            static Color VisualizeSignal(double timeSinceCreated, Color defaultColor, Color color)
            {
                if (timeSinceCreated < 1.0f)
                {
                    float pulseAmount = (MathF.Sin((float)Timing.TotalTimeUnpaused * 10.0f) + 3.0f) / 4.0f;
                    Color targetColor = Color.Lerp(defaultColor, color, pulseAmount);
                    return Color.Lerp(targetColor, defaultColor, (float)timeSinceCreated);
                }
                return defaultColor;
            }
        }

        private void DrawConnection(SpriteBatch spriteBatch, ConnectionPanel panel, Vector2 position, Vector2 labelPos)
        {
            string text = DisplayName.Value.ToUpperInvariant();

            //nasty
            if (GUIStyle.GetComponentStyle("ConnectionPanelLabel")?.Sprites.Values.First().First() is UISprite labelSprite)
            {
                Rectangle labelArea = GetLabelArea(labelPos, text);
                labelSprite.Draw(spriteBatch, labelArea, IsPower ? GUIStyle.Red : Color.SteelBlue);
            }

            GUI.DrawString(spriteBatch, labelPos + Vector2.UnitY, text, Color.Black * 0.8f, font: GUIStyle.SmallFont);
            GUI.DrawString(spriteBatch, labelPos, text, GUIStyle.TextColorBright, font: GUIStyle.SmallFont);

            float connectorSpriteScale = ConnectionSpriteSize / connectionSprite.SourceRect.Width;

            connectionSprite.Draw(spriteBatch, position, scale: connectorSpriteScale);

        }

        private void DrawWires(SpriteBatch spriteBatch, ConnectionPanel panel, Vector2 position, Vector2 wirePosition, bool mouseIn, Wire equippedWire, float wireInterval)
        {
            float connectorSpriteScale = ConnectionSpriteSize / connectionSprite.SourceRect.Width;

            foreach (var wire in wires)
            {
                if (wire.Hidden || (DraggingConnected == wire && (mouseIn || Screen.Selected == GameMain.SubEditorScreen))) { continue; }
                if (wire.HiddenInGame && Screen.Selected == GameMain.GameScreen) { continue; }

                Connection recipient = wire.OtherConnection(this);
                LocalizedString label;
                if (wire.Item.IsLayerHidden)
                {
                    label = TextManager.Get("ConnectionLocked");
                }
                else
                {
                    label = recipient == null ? "" : recipient.item.Name + $" ({recipient.DisplayName})";
                    if (wire.Locked) { label += "\n" + TextManager.Get("ConnectionLocked"); }
                }

                DrawWire(spriteBatch, wire, position, wirePosition, equippedWire, panel, label);

                wirePosition.Y += wireInterval;
            }

            bool isMouseOn = Vector2.Distance(position, PlayerInput.MousePosition) < (20.0f * GUI.Scale);
            if (isMouseOn)
            {
                connectionSpriteHighlight.Draw(spriteBatch, position, scale: connectorSpriteScale);
            }

            if (DraggingConnected != null && isMouseOn)
            {

                if (!PlayerInput.PrimaryMouseButtonHeld())
                {
                    if ((GameMain.NetworkMember != null || panel.CheckCharacterSuccess(Character.Controlled)) &&
                        Wires.Count < MaxPlayerConnectableWires)
                    {
                        //find an empty cell for the new connection
                        if (WireSlotsAvailable() && !Wires.Contains(DraggingConnected))
                        {
                            bool alreadyConnected = DraggingConnected.IsConnectedTo(panel.Item);
                            DraggingConnected.RemoveConnection(panel.Item);
                            if (DraggingConnected.TryConnect(this, !alreadyConnected, true))
                            {
                                var otherConnection = DraggingConnected.OtherConnection(this);
                                ConnectWire(DraggingConnected);
                            }
                        }
                    }

                    if (GameMain.Client != null)
                    {
                        panel.Item.CreateClientEvent(panel);
                    }
                    DraggingConnected = null;
                }
            }

            if (FlashTimer > 0.0f)
            {
                //the number of flashes depends on the duration, 1 flash per 1 full second
                int flashCycleCount = (int)Math.Max(flashDuration, 1);
                float flashCycleDuration = flashDuration / flashCycleCount;

                //MathHelper.Pi * 0.8f -> the curve goes from 144 deg to 0, 
                //i.e. quickly bumps up from almost full brightness to full and then fades out
                connectionSpriteHighlight.Draw(spriteBatch, position,
                    flashColor * (float)Math.Sin(FlashTimer % flashCycleDuration / flashCycleDuration * MathHelper.Pi * 0.8f), scale: connectorSpriteScale);
            }

            if (Wires.Any(w => w != DraggingConnected && !w.Hidden && (!w.HiddenInGame || Screen.Selected != GameMain.GameScreen)))
            {
                int screwIndex = (int)Math.Floor(position.Y / 30.0f) % screwSprites.Count;
                screwSprites[screwIndex].Draw(spriteBatch, position, scale: connectorSpriteScale);
            }
        }

        public void Flash(Color? color = null, float flashDuration = 1.5f)
        {
            FlashTimer = flashDuration;
            this.flashDuration = flashDuration;
            flashColor = (color == null) ? GUIStyle.Red : (Color)color;
        }

        public void UpdateFlashTimer(float deltaTime)
        {
            if (FlashTimer <= 0) return;
            FlashTimer -= deltaTime;
        }

        private (string signal, LocalizedString tooltip) lastSignalToolTip;
        private  (int powerValue, LocalizedString tooltip) lastPowerToolTip;

        private LocalizedString GetToolTip()
        {
            if (LastReceivedSignal.TimeSinceCreated < 1.0f)
            {
                return getSignalTooltip(LastReceivedSignal, "receivedsignal");
            }
            else if (LastSentSignal.TimeSinceCreated < 1.0f)
            {
                return getSignalTooltip(LastSentSignal, "sentsignal");
            }

            LocalizedString getSignalTooltip(Signal signal, string textTag)
            {
                if (lastSignalToolTip.signal == signal.value && !lastSignalToolTip.tooltip.IsNullOrEmpty()) { return lastSignalToolTip.tooltip; }
                lastSignalToolTip = (signal.value, TextManager.GetWithVariable(textTag, "[signal]", signal.value));
                return lastSignalToolTip.tooltip;
            }

            if (IsPower)
            {
                if (item.GetComponent<Powered>() is Powered powered)
                {
                    if (IsOutput)
                    {
                        if (powered.CurrPowerConsumption < 0)
                        {
                            return getPowerTooltip(-(int)powered.CurrPowerConsumption, "reactoroutput");
                        }
                        else if (powered is PowerTransfer || powered is PowerContainer)
                        {
                            return getPowerTooltip((int)(Grid?.Power ?? 0), "reactoroutput");
                        }
                    }
                    else if (!IsOutput)
                    {
                        float powerConsumption = powered.GetCurrentPowerConsumption(this);
                        if (!MathUtils.NearlyEqual((int)powerConsumption, 0.0f))
                        {
                            return getPowerTooltip((int)powerConsumption, "reactorload");
                        }
                    }
                }
            }

            LocalizedString getPowerTooltip(int powerValue, string textTag)
            {
                if (lastPowerToolTip.powerValue == powerValue && !lastPowerToolTip.tooltip.IsNullOrEmpty()) { return lastPowerToolTip.tooltip; }
                lastPowerToolTip = (powerValue, TextManager.GetWithVariable(textTag, "[kw]", powerValue.ToString()));
                return lastPowerToolTip.tooltip;
            }

            return null;
        }

        private static void DrawWire(SpriteBatch spriteBatch, Wire wire, Vector2 end, Vector2 start, Wire equippedWire, ConnectionPanel panel, LocalizedString label)
        {
            int textX = (int)start.X;
            if (start.X < end.X)
                textX -= 10;
            else
                textX += 10;

            bool canDrag = equippedWire == null || equippedWire == wire;

            float alpha = canDrag ? 1.0f : 0.5f;

            bool mouseOn =
                canDrag &&
                GUI.MouseOn is not GUIDragHandle &&
                ((PlayerInput.MousePosition.X > Math.Min(start.X, end.X) &&
                PlayerInput.MousePosition.X < Math.Max(start.X, end.X) &&
                MathUtils.LineToPointDistanceSquared(start, end, PlayerInput.MousePosition) < 36) ||
                Vector2.Distance(end, PlayerInput.MousePosition) < 20.0f ||
                new Rectangle((start.X < end.X) ? textX - 100 : textX, (int)start.Y - 5, 100, 14).Contains(PlayerInput.MousePosition));

            if (!label.IsNullOrEmpty())
            {
                if (start.Y > panel.GuiFrame.Rect.Bottom - 1.0f)
                {
                    //wire at the bottom of the panel -> draw the text below the panel, tilted 45 degrees
                    GUIStyle.Font.DrawString(spriteBatch, label, start + Vector2.UnitY * 20 * GUI.Scale, Color.White, 45.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0.0f);
                }
                else
                {
                    GUI.DrawString(spriteBatch,
                        new Vector2(start.X < end.X ? textX - GUIStyle.SmallFont.MeasureString(label).X : textX, start.Y - 5.0f),
                        label,
                        wire.Locked ? GUIStyle.TextColorDim : (mouseOn ? Wire.higlightColor : GUIStyle.TextColorNormal), Color.Black * 0.9f,
                        3, GUIStyle.SmallFont);
                }
            }

            var wireEnd = end + Vector2.Normalize(start - end) * 30.0f * GUI.Scale;

            float dist = Vector2.Distance(start, wireEnd);

            float wireWidth = 12 * GUI.Scale;
            float highlight = 5 * GUI.Scale;
            if (mouseOn)
            {
                spriteBatch.Draw(wireVertical.Texture, new Rectangle(wireEnd.ToPoint(), new Point((int)(wireWidth + highlight), (int)dist)), wireVertical.SourceRect,
                    Wire.higlightColor,
                    MathUtils.VectorToAngle(end - start) + MathHelper.PiOver2,
                    new Vector2(wireVertical.size.X / 2, 0), // point in line about which to rotate
                    SpriteEffects.None,
                    0.0f);
            }
            spriteBatch.Draw(wireVertical.Texture, new Rectangle(wireEnd.ToPoint(), new Point((int)wireWidth, (int)dist)), wireVertical.SourceRect,
                wire.Item.Color * alpha,
                MathUtils.VectorToAngle(end - start) + MathHelper.PiOver2,
                new Vector2(wireVertical.size.X / 2, 0), // point in line about which to rotate
                SpriteEffects.None,
                0.0f);

            float connectorScale = wireWidth / (float)wireVertical.SourceRect.Width;

            connector.Draw(spriteBatch, end, Color.White, connector.Origin, MathUtils.VectorToAngle(end - start) + MathHelper.PiOver2, scale: connectorScale);

            if (DraggingConnected == null && canDrag)
            {
                if (mouseOn)
                {
                    ConnectionPanel.HighlightedWire = wire;

                    bool allowRewiring = GameMain.NetworkMember?.ServerSettings == null || GameMain.NetworkMember.ServerSettings.AllowRewiring || panel.AlwaysAllowRewiring;
                    if (allowRewiring && (!wire.Locked && !wire.Item.IsLayerHidden && !panel.Locked && !panel.TemporarilyLocked || Screen.Selected == GameMain.SubEditorScreen))
                    {
                        //start dragging the wire
                        if (PlayerInput.PrimaryMouseButtonHeld()) { DraggingConnected = wire; }
                    }
                }
            }
        }

        public static bool CheckConnectionLabelOverlap(ConnectionPanel panel, out Point newRectSize)
        {
            Rectangle panelRect = panel.GuiFrame.Rect;
            int x = panelRect.X, y = panelRect.Y;
            Vector2 rightPos = GetRightPos(x, y, panelRect.Width);
            Vector2 leftPos = GetLeftPos(x, y);
            int connectorIntervalLeft = GetConnectorIntervalLeft(panelRect.Height, panel);
            int connectorIntervalRight = GetConnectorIntervalRight(panelRect.Height, panel);
            newRectSize = panelRect.Size;

            //make sure the connection labels don't overlap horizontally
            float rightMostInput = panelRect.Center.X;
            float leftMostOutput = panelRect.Center.X;
            foreach (var c in panel.Connections)
            {
                if (c.IsOutput)
                {
                    var labelArea = GetLabelArea(GetOutputLabelPosition(rightPos, panel, c), c.DisplayName.Value.ToUpperInvariant());
                    leftMostOutput = Math.Min(leftMostOutput, labelArea.X);
                    rightPos.Y += connectorIntervalLeft;
                }
                else
                {
                    var labelArea = GetLabelArea(GetInputLabelPosition(leftPos, panel, c), c.DisplayName.Value.ToUpperInvariant());
                    rightMostInput = Math.Max(rightMostInput, labelArea.Right);
                    leftPos.Y += connectorIntervalRight;
                }
            }
            if (leftMostOutput < rightMostInput)
            {
                newRectSize += new Point((int)(rightMostInput - leftMostOutput) + GUI.IntScale(15), 0);
            }

            //make sure connection sprites don't overlap vertically
            while (GetConnectorIntervalLeft(newRectSize.Y, panel) < ConnectionSpriteSize ||
                    GetConnectorIntervalRight(newRectSize.Y, panel) < ConnectionSpriteSize)
            {
                newRectSize.Y += 10;
            }
            return newRectSize.X != panel.GuiFrame.Rect.Width || newRectSize.Y > panel.GuiFrame.Rect.Height;
        }

        private static Vector2 GetInputLabelPosition(Vector2 connectorPosition, ConnectionPanel panel, Connection connection)
        {
            return new Vector2(
                connectorPosition.X + 25 * GUI.Scale,
                connectorPosition.Y - GUIStyle.SmallFont.MeasureString(connection.DisplayName.ToUpper()).Y / 2);
        }

        private static Vector2 GetOutputLabelPosition(Vector2 connectorPosition, ConnectionPanel panel, Connection connection)
        {
            return new Vector2(
                connectorPosition.X - 25 * GUI.Scale - GUIStyle.SmallFont.MeasureString(connection.DisplayName.ToUpper()).X,
                connectorPosition.Y - GUIStyle.SmallFont.MeasureString(connection.DisplayName.ToUpper()).Y / 2);
        }

        private static Rectangle GetLabelArea(Vector2 labelPos, string text)
        {
            Vector2 textSize = GUIStyle.SmallFont.MeasureString(text);
            Rectangle labelArea = new Rectangle(labelPos.ToPoint(), textSize.ToPoint());
            labelArea.Inflate(GUI.IntScale(10), GUI.IntScale(3));
            return labelArea;
        }

        private static Vector2 GetLeftPos(int x, int y)
        {
            return new Vector2(x + 80 * GUI.Scale, y + 60 * GUI.Scale);
        }

        private static Vector2 GetRightPos(int x, int y, int width)
        {
            return new Vector2(x + width - 80 * GUI.Scale, y + 60 * GUI.Scale);
        }

        private static int GetConnectorIntervalLeft(int height, ConnectionPanel panel)
        {
            return (height - GUI.IntScale(60)) / Math.Max(panel.Connections.Count(c => c.IsOutput), 1);
        }

        private static int GetConnectorIntervalRight(int height, ConnectionPanel panel)
        {
            return (height - GUI.IntScale(60)) / Math.Max(panel.Connections.Count(c => !c.IsOutput), 1);
        }
    }
}
