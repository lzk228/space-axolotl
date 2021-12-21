﻿using System;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;

namespace Content.Shared.Inventory;

public abstract partial class InventorySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    private void InitializeEquip()
    {
        //these events ensure that the client also gets its proper events raised when getting its containerstate updated
        SubscribeLocalEvent<InventoryComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<InventoryComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void OnEntRemoved(EntityUid uid, InventoryComponent component, EntRemovedFromContainerMessage args)
    {
        if(!TryGetSlot(uid, args.Container.ID, out var slotDef, inventory: component))
            return;

        var unequippedEvent = new DidUnequipEvent(uid, args.Entity, slotDef);
        RaiseLocalEvent(uid, unequippedEvent);

        var gotUnequippedEvent = new GotUnequippedEvent(uid, args.Entity, slotDef);
        RaiseLocalEvent(args.Entity, gotUnequippedEvent);
    }

    private void OnEntInserted(EntityUid uid, InventoryComponent component, EntInsertedIntoContainerMessage args)
    {
        if(!TryGetSlot(uid, args.Container.ID, out var slotDef, inventory: component))
           return;

        var equippedEvent = new DidEquipEvent(uid, args.Entity, slotDef);
        RaiseLocalEvent(uid, equippedEvent);

        var gotEquippedEvent = new GotEquippedEvent(uid, args.Entity, slotDef);
        RaiseLocalEvent(args.Entity, gotEquippedEvent);
    }

    public bool TryEquipActiveHandTo(EntityUid uid, string slot, bool silent = false, bool force = false,
        InventoryComponent? component = null, SharedHandsComponent? hands = null)
    {
        if (!Resolve(uid, ref component, false) || !Resolve(uid, ref hands, false))
            return false;

        if (!hands.TryGetActiveHeldEntity(out var heldEntity))
            return false;

        return TryEquip(uid, heldEntity, slot, silent, force, component);
    }

    public virtual bool TryEquip(EntityUid uid, EntityUid itemUid, string slot, bool silent = false, bool force = false, InventoryComponent? inventory = null, SharedItemComponent? item = null)
    {
        if (!Resolve(uid, ref inventory, false) || !Resolve(itemUid, ref item, false))
        {
            if(!silent) _popup.PopupCursor(Loc.GetString("inventory-component-can-equip-cannot"), Filter.Local());
            return false;
        }

        if (!TryGetSlotContainer(uid, slot, out var slotContainer, out var slotDefinition, inventory))
        {
            if(!silent) _popup.PopupCursor(Loc.GetString("inventory-component-can-equip-cannot"), Filter.Local());
            return false;
        }

        if (!force && !CanEquip(uid, itemUid, slot, out var reason, slotDefinition, inventory, item))
        {
            if(!silent) _popup.PopupCursor(Loc.GetString(reason), Filter.Local());
            return false;
        }

        if (!slotContainer.Insert(itemUid))
        {
            if(!silent)  _popup.PopupCursor(Loc.GetString("inventory-component-can-unequip-cannot"), Filter.Local());
            return false;
        }

        if(item.EquipSound != null)
            SoundSystem.Play(Filter.Pvs(uid), item.EquipSound.GetSound(), uid, AudioParams.Default.WithVolume(-2f));

        inventory.Dirty();

        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        return true;
    }

    public bool CanEquip(EntityUid uid, EntityUid itemUid, string slot, [NotNullWhen(false)] out string? reason, SlotDefinition? slotDefinition = null, InventoryComponent? inventory = null, SharedItemComponent? item = null)
    {
        reason = "inventory-component-can-equip-cannot";
        if (!Resolve(uid, ref inventory, false) || !Resolve(itemUid, ref item, false))
            return false;

        if (slotDefinition == null && !TryGetSlot(uid, slot, out slotDefinition, inventory: inventory))
            return false;

        if (slotDefinition.DependsOn != null && !TryGetSlotEntity(uid, slotDefinition.DependsOn, out _, inventory))
            return false;

        if(!item.SlotFlags.HasFlag(slotDefinition.SlotFlags) && (!slotDefinition.SlotFlags.HasFlag(SlotFlags.POCKET) || item.Size > (int) ReferenceSizes.Pocket))
        {
            reason = "inventory-component-can-equip-does-not-fit";
            return false;
        }

        var attemptEvent = new IsEquippingAttemptEvent(uid, itemUid, slotDefinition);
        RaiseLocalEvent(uid, attemptEvent);
        if (attemptEvent.Cancelled)
        {
            reason = attemptEvent.Reason ?? reason;
            return false;
        }

        var itemAttemptEvent = new BeingEquippedAttemptEvent(uid, itemUid, slotDefinition);
        RaiseLocalEvent(itemUid, itemAttemptEvent);
        if (itemAttemptEvent.Cancelled)
        {
            reason = itemAttemptEvent.Reason ?? reason;
            return false;
        }

        return true;
    }

    public virtual bool TryUnequip(EntityUid uid, string slot, bool silent = false, bool force = false,
        InventoryComponent? inventory = null)
    {
        if (!Resolve(uid, ref inventory, false))
        {
            if(!silent) _popup.PopupCursor(Loc.GetString("inventory-component-can-unequip-cannot"), Filter.Local());
            return false;
        }

        if (!TryGetSlotContainer(uid, slot, out var slotContainer, out var slotDefinition, inventory))
        {
            if(!silent) _popup.PopupCursor(Loc.GetString("inventory-component-can-unequip-cannot"), Filter.Local());
            return false;
        }

        var entity = slotContainer.ContainedEntity;

        if (!entity.HasValue) return false;

        if (!force && !CanUnequip(uid, slot, out var reason, slotContainer, slotDefinition, inventory))
        {
            if(!silent) _popup.PopupCursor(Loc.GetString(reason), Filter.Local());
            return false;
        }

        //we need to do this to make sure we are 100% removing this entity, since we are now dropping dependant slots
        if (!force && !slotContainer.CanRemove(entity.Value))
            return false;

        foreach (var slotDef in GetSlots(uid, inventory))
        {
            if (slotDef != slotDefinition && slotDef.DependsOn == slotDefinition.Name)
            {
                //this recursive call might be risky
                TryUnequip(uid, slotDef.Name, true, true, inventory);
            }
        }

        if (force)
        {
            slotContainer.ForceRemove(entity.Value);
        }
        else
        {
            if (!slotContainer.Remove(entity.Value))
            {
                //should never happen bc of the cabut lets just keep in just in case
                return false;
            }
        }

        Transform(entity.Value).Coordinates = EntityManager.GetComponent<TransformComponent>(uid).Coordinates;

        inventory.Dirty();

        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        return true;
    }

    public bool CanUnequip(EntityUid uid, string slot, [NotNullWhen(false)] out string? reason, ContainerSlot? containerSlot = null, SlotDefinition? slotDefinition = null, InventoryComponent? inventory = null)
    {
        reason = "inventory-component-can-unequip-cannot";
        if (!Resolve(uid, ref inventory, false))
            return false;

        if ((containerSlot == null || slotDefinition == null) && !TryGetSlotContainer(uid, slot, out containerSlot, out slotDefinition, inventory))
            return false;

        if (containerSlot.ContainedEntity == null)
            return false;

        if (!containerSlot.ContainedEntity.HasValue || !containerSlot.CanRemove(containerSlot.ContainedEntity.Value))
            return false;

        var itemUid = containerSlot.ContainedEntity.Value;

        var attemptEvent = new IsUnequippingAttemptEvent(uid, itemUid, slotDefinition);
        RaiseLocalEvent(uid, attemptEvent);
        if (attemptEvent.Cancelled)
        {
            reason = attemptEvent.Reason ?? reason;
            return false;
        }

        var itemAttemptEvent = new BeingUnequippedAttemptEvent(uid, itemUid, slotDefinition);
        RaiseLocalEvent(itemUid, itemAttemptEvent);
        if (itemAttemptEvent.Cancelled)
        {
            reason = attemptEvent.Reason ?? reason;
            return false;
        }

        return true;
    }

    public bool TryGetSlotEntity(EntityUid uid, string slot, [NotNullWhen(true)] out EntityUid? entityUid, InventoryComponent? inventoryComponent = null, ContainerManagerComponent? containerManagerComponent = null)
    {
        entityUid = null;
        if (!Resolve(uid, ref inventoryComponent, ref containerManagerComponent, false)
            || !TryGetSlotContainer(uid, slot, out var container, out _, inventoryComponent, containerManagerComponent))
            return false;

        entityUid = container.ContainedEntity;
        return entityUid != null;
    }
}
