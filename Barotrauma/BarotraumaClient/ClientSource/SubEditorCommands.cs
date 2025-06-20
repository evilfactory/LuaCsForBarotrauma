﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;

namespace Barotrauma
{
    internal readonly struct InventorySlotItem
    {
        public readonly int Slot;
        public readonly Item Item;

        public InventorySlotItem(int slot, Item item)
        {
            Slot = slot;
            Item = item;
        }

        public void Deconstruct(out int slot, out Item item)
        {
            slot = Slot;
            item = Item;
        }
    }

    internal abstract partial class Command
    {
        public abstract LocalizedString GetDescription();
    }

    /// <summary>
    /// A command for setting and reverting a MapEntity rectangle
    /// <see cref="SubEditorScreen"/>
    /// <see cref="MapEntity"/>
    /// </summary>
    internal class TransformCommand : Command
    {
        private readonly List<MapEntity> Receivers;
        private readonly List<Rectangle> NewData;
        private readonly List<Rectangle> OldData;
        private readonly bool Resized;

        /// <summary>
        /// A command for setting and reverting a MapEntity rectangle
        /// </summary>
        /// <param name="receivers">Entities whose rectangle has been altered</param>
        /// <param name="newData">The new rectangle that is or will be applied to the map entity</param>
        /// <param name="oldData">Old rectangle the map entity had before</param>
        /// <param name="resized">If the transform was resized or not</param>
        /// <remarks>
        /// All lists should be equal in length, for every receiver there should be a corresponding entry at the same position in newData and oldData.
        /// </remarks>
        public TransformCommand(List<MapEntity> receivers, List<Rectangle> newData, List<Rectangle> oldData, bool resized)
        {
            Receivers = receivers;
            NewData = newData;
            OldData = oldData;
            Resized = resized;
        }

        public override void Execute() => SetRects(NewData);
        public override void UnExecute() => SetRects(OldData);

        public override void Cleanup()
        {
            NewData.Clear();
            OldData.Clear();
            Receivers.Clear();
        }

        private void SetRects(IReadOnlyList<Rectangle> rects)
        {
            if (Receivers.Count != rects.Count)
            {
                DebugConsole.ThrowError($"Receivers.Count did not match Rects.Count ({Receivers.Count} vs {rects.Count}).");
                return;
            }

            for (int i = 0; i < rects.Count; i++)
            {
                MapEntity entity = Receivers[i].GetReplacementOrThis();
                Rectangle Rect = rects[i];
                Vector2 diff = Rect.Location.ToVector2() - entity.Rect.Location.ToVector2();
                entity.Move(diff);
                entity.Rect = Rect;
            }
        }

        public override LocalizedString GetDescription()
        {
            if (Resized)
            {
                return TextManager.GetWithVariable("Undo.ResizedItem", "[item]", Receivers.FirstOrDefault()?.Name);
            }

            return Receivers.Count > 1
                ? TextManager.GetWithVariable("Undo.MovedItemsMultiple", "[count]", Receivers.Count.ToString())
                : TextManager.GetWithVariable("Undo.MovedItem", "[item]", Receivers.FirstOrDefault()?.Name);
        }
    }

    /// <summary>
    /// A command that removes and unremoves map entities
    /// <see cref="ItemPrefab"/>
    /// <see cref="StructurePrefab"/>
    /// <seealso cref="SubEditorScreen"/>
    /// </summary>
    internal class AddOrDeleteCommand : Command
    {
        private readonly Dictionary<InventorySlotItem, Inventory> PreviousInventories = new Dictionary<InventorySlotItem, Inventory>();
        public readonly List<MapEntity> Receivers;
        private readonly List<MapEntity> CloneList;
        private readonly bool WasDeleted;
        private readonly List<AddOrDeleteCommand> ContainedItemsCommand = new List<AddOrDeleteCommand>();

        // We need to 'snapshot' the state of the circuit box and the best way to do that is to save it to XML. 
        private readonly List<XElement> CircuitBoxData = new List<XElement>();

        /// <summary>
        /// Creates a command where all entities share the same state.
        /// </summary>
        /// <param name="receivers">Entities that were deleted or added</param>
        /// <param name="wasDeleted">Whether or not all entities are or are going to be deleted</param>
        /// <param name="handleInventoryBehavior">Ignore item inventories when set to false, workaround for pasting</param>
        public AddOrDeleteCommand(List<MapEntity> receivers, bool wasDeleted, bool handleInventoryBehavior = true)
        {
            Debug.Assert(receivers.Count > 0, "Command has 0 receivers");
            WasDeleted = wasDeleted;
            Receivers = new List<MapEntity>(receivers);

            try
            {
                foreach (MapEntity receiver in receivers)
                {
                    if (receiver is Item { ParentInventory: not null } it)
                    {
                        PreviousInventories.Add(new InventorySlotItem(it.ParentInventory.FindIndex(it), it), it.ParentInventory);
                    }
                }

                List<MapEntity> clonedTargets = MapEntity.Clone(receivers);

                List<MapEntity> itemsToDelete = new List<MapEntity>();
                foreach (MapEntity receiver in Receivers)
                {
                    if (receiver is not Item it) { continue; }

                    foreach (var cb in it.GetComponents<CircuitBox>())
                    {
                        CircuitBoxData.Add(cb.Save(new XElement("root")));
                    }

                    foreach (ItemContainer component in it.GetComponents<ItemContainer>())
                    {
                        if (component.Inventory == null) { continue; }
                        itemsToDelete.AddRange(component.Inventory.AllItems.Where(static item => !item.Removed));
                    }
                }

                if (itemsToDelete.Any() && handleInventoryBehavior)
                {
                    ContainedItemsCommand.Add(new AddOrDeleteCommand(itemsToDelete, wasDeleted));
                    if (wasDeleted)
                    {
                        foreach (MapEntity item in itemsToDelete)
                        {
                            if (item != null && !item.Removed)
                            {
                                item.Remove();
                            }
                        }
                    }
                }

                foreach (MapEntity clone in clonedTargets)
                {
                    clone.ShallowRemove();
                    if (clone is Item it)
                    {
                        foreach (ItemContainer container in it.GetComponents<ItemContainer>())
                        {
                            container.Inventory?.DeleteAllItems();
                        }
                    }
                }

                CloneList = clonedTargets;
            }
            // This should never happen except if we decide to make a new type of MapEntity that isn't finished yet
            catch (Exception e)
            {
                Receivers = new List<MapEntity>();
                CloneList = new List<MapEntity>();
                DebugConsole.ThrowError("Could not store object", e);
            }
        }

        public override void Execute()
            => Process(true);

        public override void UnExecute()
            => Process(false);

        private void Process(bool redo)
        {
            var items = DeleteUndelete(redo);
            foreach (var cmd in ContainedItemsCommand)
            {
                cmd.Process(redo);
            }
            ApplyCircuitBoxDataIfAny(items);
        }

        /// <summary>
        /// We need to manually copy over the circuit box data because of how the undo handles inventory items.
        /// The undo system recursively deletes inventory items and creates a separate command for each one.
        /// This causes the circuit box to lose its internal inventory when it's cloned and then restored and make it
        /// unable to load the state from XML.
        ///
        /// The workaround to this is to ignore the XML that is being loaded when the item is created and instead
        /// save the XML into the command and then load it back after the undo system has restored the items which
        /// is what this function does.
        /// </summary>
        private void ApplyCircuitBoxDataIfAny(ImmutableArray<Item> items)
        {
            int cbIndex = 0;
            foreach (var newItem in items)
            {
                foreach (ItemComponent component in newItem.Components)
                {
                    if (component is not CircuitBox cb) { continue; }

                    if (cbIndex < 0 || cbIndex >= CircuitBoxData.Count)
                    {
                        DebugConsole.ThrowError("Unable to restore wiring in circuit box, index out of range.");
                        continue;
                    }

                    var cbData = CircuitBoxData[cbIndex];
                    cbIndex++;

                    cb.LoadFromXML(new ContentXElement(null, cbData));
                }
            }
        }

        public override void Cleanup()
        {
            foreach (MapEntity entity in CloneList)
            {
                if (!entity.Removed)
                {
                    entity.Remove();
                }
            }

            CloneList?.Clear();
            Receivers.Clear();
            PreviousInventories?.Clear();
            ContainedItemsCommand?.ForEach(static cmd => cmd.Cleanup());
            CircuitBoxData.Clear();
        }

        private ImmutableArray<Item> DeleteUndelete(bool redo)
        {
            bool wasDeleted = WasDeleted;

            // We are redoing instead of undoing, flip the behavior
            if (redo) { wasDeleted = !wasDeleted; }

            // collect newly created items so we can update their circuit boxes if any
            var builder = ImmutableArray.CreateBuilder<Item>();

            if (wasDeleted)
            {
                Debug.Assert(Receivers.All(static entity => entity.GetReplacementOrThis().Removed), "Tried to redo a deletion but some items were not deleted");

                List<MapEntity> clones = MapEntity.Clone(CloneList);
                int length = Math.Min(Receivers.Count, clones.Count);
                for (int i = 0; i < length; i++)
                {
                    MapEntity clone = clones[i],
                              receiver = Receivers[i];

                    if (receiver.GetReplacementOrThis() is Item item && clone is Item cloneItem)
                    {
                        builder.Add(cloneItem);
                        foreach (ItemComponent ic in item.Components)
                        {
                            int index = item.GetComponentIndex(ic);
                            ItemComponent component = cloneItem.Components.ElementAtOrDefault(index);
                            switch (component)
                            {
                                case null:
                                    continue;
                                case ItemContainer { Inventory: not null } newContainer when ic is ItemContainer { Inventory: not null } itemContainer:
                                    itemContainer.Inventory.GetReplacementOrThis().ReplacedBy = newContainer.Inventory;
                                    goto default;
                                default:
                                    ic.GetReplacementOrThis().ReplacedBy = component;
                                    break;
                            }
                        }
                    }

                    receiver.GetReplacementOrThis().ReplacedBy = clone;
                }

                for (int i = 0; i < length; i++)
                {
                    MapEntity clone = clones[i],
                              receiver = Receivers[i];

                    if (clone is Item it)
                    {
                        foreach (var (slotRef, inventory) in PreviousInventories)
                        {
                            if (slotRef.Item == receiver)
                            {
                                inventory.GetReplacementOrThis().TryPutItem(it, slotRef.Slot, false, false, null, createNetworkEvent: false);
                            }
                        }
                    }
                }

                foreach (MapEntity clone in clones)
                {
                    clone.Submarine = Submarine.MainSub;
                }

                return builder.ToImmutable();
            }
            else
            {
                foreach (MapEntity t in Receivers)
                {
                    MapEntity receiver = t.GetReplacementOrThis();
                    if (!receiver.Removed)
                    {
                        receiver.Remove();
                    }
                }

                return builder.ToImmutable();
            }
        }

        public void MergeInto(AddOrDeleteCommand master)
        {
            master.Receivers.AddRange(Receivers);
            master.CloneList.AddRange(CloneList);
            master.ContainedItemsCommand.AddRange(ContainedItemsCommand);
            foreach (var (slot, item) in PreviousInventories)
            {
                master.PreviousInventories.Add(slot, item);
            }
        }

        public override LocalizedString GetDescription()
        {
            if (WasDeleted)
            {
                return Receivers.Count > 1
                    ? TextManager.GetWithVariable("Undo.RemovedItemsMultiple", "[count]", Receivers.Count.ToString())
                    : TextManager.GetWithVariable("Undo.RemovedItem", "[item]", Receivers.FirstOrDefault()?.Name ?? "null");
            }

            return Receivers.Count > 1
                ? TextManager.GetWithVariable("Undo.AddedItemsMultiple", "[count]", Receivers.Count.ToString())
                : TextManager.GetWithVariable("Undo.AddedItem", "[item]", Receivers.FirstOrDefault()?.Name ?? "null");
        }
    }

    /// <summary>
    /// A command that places or drops items out of inventories
    /// </summary>
    /// <see cref="Inventory"/>
    /// <see cref="MapEntity"/>
    internal class InventoryPlaceCommand : Command
    {
        private readonly Inventory Inventory;
        private readonly List<InventorySlotItem> Receivers;
        private readonly bool wasDropped;

        public InventoryPlaceCommand(Inventory inventory, List<Item> items, bool dropped)
        {
            Inventory = inventory;
            Receivers = items.Select(item => new InventorySlotItem(inventory.FindIndex(item), item)).ToList();
            wasDropped = dropped;
        }

        public override void Execute() => ContainUncontain(false);
        public override void UnExecute() => ContainUncontain(true);

        public override void Cleanup()
        {
            Receivers.Clear();
        }

        private void ContainUncontain(bool drop)
        {
            // flip the behavior if the item was dropped instead of inserted
            if (wasDropped) { drop = !drop; }

            foreach (var (slot, receiver) in Receivers)
            {
                Item item = (Item) receiver.GetReplacementOrThis();

                if (drop)
                {
                    item.Drop(null, createNetworkEvent: false);
                }
                else
                {
                    Inventory.GetReplacementOrThis().TryPutItem(item, slot, false, false, null, createNetworkEvent: false);
                }
            }
        }

        public override LocalizedString GetDescription()
        {
            if (wasDropped)
            {
                return TextManager.GetWithVariable("Undo.DroppedItem", "[item]", Receivers.FirstOrDefault().Item.Name);
            }

            string container = "[ERROR]";

            if (Inventory.Owner is Item item)
            {
                container = item.Name;
            }

            return Receivers.Count > 1
                ? TextManager.GetWithVariables("Undo.ContainedItemsMultiple", ("[count]", Receivers.Count.ToString()), ("[container]", container))
                : TextManager.GetWithVariables("Undo.ContainedItem", ("[item]", Receivers.FirstOrDefault().Item.Name), ("[container]", container));
        }
    }

    /// <summary>
    /// A command that sets item properties
    /// </summary>
    internal class PropertyCommand : Command
    {
        private Dictionary<object, List<ISerializableEntity>> OldProperties;
        private readonly List<ISerializableEntity> Receivers;
        private readonly Identifier PropertyName;
        private readonly object NewProperties;
        private string sanitizedProperty;

        public readonly int PropertyCount;

        /// <summary>
        /// A command that sets item properties
        /// </summary>
        /// <param name="receivers">Affected entities</param>
        /// <param name="propertyName">Real property name, not all lowercase</param>
        /// <param name="newData"></param>
        /// <param name="oldData"></param>
        public PropertyCommand(List<ISerializableEntity> receivers, Identifier propertyName, object newData, Dictionary<object, List<ISerializableEntity>> oldData)
        {
            Receivers = receivers;
            PropertyName = propertyName;
            OldProperties = oldData;
            NewProperties = newData;
            PropertyCount = receivers.Count;
            SanitizeProperty();
        }

        public PropertyCommand(ISerializableEntity receiver, Identifier propertyName, object newData, object oldData)
        {
            Receivers = new List<ISerializableEntity> { receiver };
            PropertyName = propertyName;
            OldProperties = new Dictionary<object, List<ISerializableEntity>> { { oldData, Receivers } };
            NewProperties = newData;
            PropertyCount = 1;
            SanitizeProperty();
        }

        public bool MergeInto(PropertyCommand master)
        {
            if (!master.Receivers.SequenceEqual(Receivers)) { return false; }
            master.OldProperties = OldProperties;
            return true;
        }

        private void SanitizeProperty()
        {
            sanitizedProperty = NewProperties switch
            {
                float f => f.FormatSingleDecimal(),
                Point point => XMLExtensions.PointToString(point),
                Vector2 vector2 => vector2.FormatZeroDecimal(),
                Vector3 vector3 => vector3.FormatSingleDecimal(),
                Vector4 vector4 => vector4.FormatSingleDecimal(),
                Color color => XMLExtensions.ColorToString(color),
                Rectangle rectangle => XMLExtensions.RectToString(rectangle),
                _ => NewProperties.ToString()
            };
        }

        public override void Execute() => SetProperties(false);
        public override void UnExecute() => SetProperties(true);

        public override void Cleanup()
        {
            Receivers.Clear();
            OldProperties.Clear();
        }

        private void SetProperties(bool undo)
        {
            foreach (ISerializableEntity t in Receivers)
            {
                ISerializableEntity receiver;
                switch (t)
                {
                    case MapEntity me when me.GetReplacementOrThis() is ISerializableEntity sEntity:
                        receiver = sEntity;
                        break;
                    case ItemComponent ic when ic.GetReplacementOrThis() is ISerializableEntity sItemComponent:
                        receiver = sItemComponent;
                        break;
                    default:
                        receiver = t;
                        break;
                }

                object data = NewProperties;

                if (undo)
                {
                    foreach (var (key, value) in OldProperties)
                    {
                        if (value.Contains(t)) { data = key; }
                    }
                }

                if (receiver.SerializableProperties != null)
                {
                    Dictionary<Identifier, SerializableProperty> props = receiver.SerializableProperties;

                    if (props.TryGetValue(PropertyName, out SerializableProperty prop))
                    {
                        prop.TrySetValue(receiver, data);
                        // Update the editing hud
                        if (MapEntity.EditingHUD == null || (MapEntity.EditingHUD.UserData != receiver && (receiver is ItemComponent ic && MapEntity.EditingHUD.UserData != ic.Item))) { continue; }

                        GUIListBox list = MapEntity.EditingHUD.GetChild<GUIListBox>();
                        if (list == null) { continue; }

                        IEnumerable<SerializableEntityEditor> editors = list.Content.FindChildren(comp => comp is SerializableEntityEditor).Cast<SerializableEntityEditor>();
                        SerializableEntityEditor.LockEditing = true;
                        foreach (SerializableEntityEditor editor in editors)
                        {
                            if (editor.UserData == receiver && editor.Fields.TryGetValue(PropertyName, out GUIComponent[] _))
                            {
                                editor.UpdateValue(prop, data);
                            }
                        }

                        SerializableEntityEditor.LockEditing = false;
                    }
                }
            }
        }

        public override LocalizedString GetDescription()
        {
            return Receivers.Count > 1
                ? TextManager.GetWithVariables("Undo.ChangedPropertyMultiple",
                    ("[property]", PropertyName.Value),
                    ("[count]", Receivers.Count.ToString()),
                    ("[value]", sanitizedProperty))
                : TextManager.GetWithVariables("Undo.ChangedProperty",
                    ("[property]", PropertyName.Value),
                    ("[item]", Receivers.FirstOrDefault()?.Name),
                    ("[value]", sanitizedProperty));
        }
    }

    /// <summary>
    /// A command that moves items around in inventories
    /// </summary>
    /// <see cref="oldInventory"/>
    /// <see cref="MapEntity"/>
    internal class InventoryMoveCommand : Command
    {
        private readonly Inventory oldInventory;
        private readonly Inventory newInventory;
        private readonly int oldSlot;
        private readonly int newSlot;
        private readonly Item targetItem;

        public InventoryMoveCommand(Inventory oldInventory, Inventory newInventory, Item item, int oldSlot, int newSlot)
        {
            this.newInventory = newInventory;
            this.oldInventory = oldInventory;
            this.oldSlot = oldSlot;
            this.newSlot = newSlot;
            targetItem = item;
        }

        public override void Execute()
        {
            if (targetItem.GetReplacementOrThis() is Item item)
            {
                newInventory?.GetReplacementOrThis().TryPutItem(item, newSlot, true, false, null, createNetworkEvent: false);
            }
        }

        public override void UnExecute()
        {
            if (targetItem.GetReplacementOrThis() is Item item)
            {
                oldInventory?.GetReplacementOrThis().TryPutItem(item, oldSlot, true, false, null, createNetworkEvent: false);
            }
        }

        public override void Cleanup() { }

        public override LocalizedString GetDescription()
        {
            return TextManager.GetWithVariable("Undo.MovedItem", "[item]", targetItem.Name);
        }
    }

    /// <summary>
    /// A command for applying changes from the <see cref="SubEditorScreen.TransformWidget"/>.
    /// </summary>
    internal class TransformToolCommand : Command
    {
        private readonly Dictionary<MapEntity, SubEditorScreen.TransformData> originalData;
        public float? ScaleMult, RotationRad;
        public readonly Vector2 Pivot;
        private readonly Vector2 wirePivot;
        public float MinScale, MaxScale;

        public TransformToolCommand(Dictionary<MapEntity, SubEditorScreen.TransformData> data, Vector2 pivot)
        {
            originalData = data;
            Pivot = pivot;
            wirePivot = Pivot - Submarine.MainSub.HiddenSubPosition;

            MinScale = 0.01f / Math.Max(data.Values.Min(data => data.Scale), 0.01f);
            MaxScale = 10f / Math.Min(data.Values.Max(data => data.Scale), 10f);
        }

        public override void Execute() => UpdateTransforms(RotationRad ?? 0f, ScaleMult ?? 1f);
        public override void UnExecute() => UpdateTransforms(0f, 1f);

        public override void Cleanup() => originalData.Clear();

        private void UpdateTransforms(float rotationRad, float scaleMult)
        {
            foreach ((MapEntity receiver, SubEditorScreen.TransformData data) in originalData)
            {
                if (RotationRad.HasValue && receiver is Item { Prefab.AllowRotatingInEditor: true } or Structure { Prefab.AllowRotatingInEditor: true })
                {
                    int rotationDir = receiver is Structure && receiver.FlippedX ^ receiver.FlippedY ? -1 : 1;
                    float newRotation = MathHelper.ToDegrees(data.RotationRad + rotationRad * rotationDir);
                    switch (receiver)
                    {
                        case Item item:
                            item.Rotation = newRotation;
                            break;
                        case Structure structure:
                            structure.Rotation = newRotation;
                            break;
                    }
                    data.TurretLimits?.ForEach(pair => pair.Key.RotationLimits = pair.Value + new Vector2(MathHelper.ToDegrees(rotationRad)));
                }

                if (ScaleMult.HasValue)
                {
                    receiver.Scale = data.Scale * scaleMult;
                    if (receiver.ResizeVertical || receiver.ResizeHorizontal)
                    {
                        if (receiver.ResizeVertical)
                        {
                            receiver.RectHeight = (int)(data.Rect.Height * scaleMult);
                        }
                        if (receiver.ResizeHorizontal)
                        {
                            receiver.RectWidth = (int)(data.Rect.Width * scaleMult);
                        }
                        if (receiver is Structure structure && data.TexOffset.HasValue)
                        {
                            structure.TextureOffset = data.TexOffset.Value * scaleMult;
                        }
                    }
                    data.TextScales?.ForEach(pair => pair.Key.TextScale = pair.Value * scaleMult);
                    data.LightRanges?.ForEach(pair => pair.Key.Range = pair.Value * scaleMult);
                    data.Wires?.ForEach(pair => pair.Key.Width = pair.Value.Width * scaleMult);
                }

                Vector2 newEntityPos = MathUtils.RotatePoint((data.Pos - Pivot) * scaleMult, -rotationRad) + Pivot;
                receiver.Move(newEntityPos - receiver.DrawPosition);

                data.Wires?.ForEach(pair => pair.Key.SetNodes(pair.Value.Nodes.Select(TransformWireNode)));
                Vector2 TransformWireNode(Vector2 node) => MathUtils.RotatePoint((node - wirePivot) * scaleMult, -rotationRad) + wirePivot;
            }
        }

        public override LocalizedString GetDescription() => originalData.Count > 1
            ? TextManager.GetWithVariable("Undo.ChangedTransformMultiple", "[amount]", originalData.Count.ToString())
            : TextManager.GetWithVariable("Undo.ChangedTransform", "[item]", originalData.First().Key.Name);
    }
}