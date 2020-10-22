﻿#nullable enable
using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Items.Clothing;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Server.GameObjects.Components.Power;
using Content.Shared.GameObjects.Components;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Interactable
{
    /// <summary>
    ///     Component that represents a powered handheld light source which can be toggled on and off.
    /// </summary>
    [RegisterComponent]
    internal sealed class HandheldLightComponent : SharedHandheldLightComponent, IUse, IExamine, IInteractUsing
    {
        [ViewVariables(VVAccess.ReadWrite)] public float Wattage = 10f;
        [ViewVariables] private PowerCellSlotComponent _cellSlot = default!;
        private PowerCellComponent? Cell => _cellSlot.Cell;

        /// <summary>
        ///     Status of light, whether or not it is emitting light.
        /// </summary>
        [ViewVariables]
        public bool Activated { get; private set; }

        [ViewVariables] protected override bool HasCell => _cellSlot.HasCell;

        [ViewVariables(VVAccess.ReadWrite)] public string? TurnOnSound;
        [ViewVariables(VVAccess.ReadWrite)] public string? TurnOnFailSound;
        [ViewVariables(VVAccess.ReadWrite)] public string? TurnOffSound;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref Wattage, "wattage", 10f);
            serializer.DataField(ref TurnOnSound, "turnOnSound", "/Audio/Items/flashlight_toggle.ogg");
            serializer.DataField(ref TurnOnFailSound, "turnOnFailSound", "/Audio/Machines/button.ogg");
            serializer.DataField(ref TurnOffSound, "turnOffSound", "/Audio/Items/flashlight_toggle.ogg");
        }

        public override void Initialize()
        {
            base.Initialize();

            Owner.EnsureComponent<PointLightComponent>();
            _cellSlot = Owner.EnsureComponent<PowerCellSlotComponent>();

            Dirty();
        }

        public override void HandleMessage(ComponentMessage message, IComponent? component)
        {
            base.HandleMessage(message, component);
            switch (message)
            {
                case PowerCellChangedMessage _:
                    if (component is PowerCellSlotComponent slotComponent && slotComponent == _cellSlot)
                    {
                        Dirty();
                    }
                    break;
            }
        }

        async Task<bool> IInteractUsing.InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (!ActionBlockerSystem.CanInteract(eventArgs.User)) return false;
            if (!_cellSlot.InsertCell(eventArgs.Using)) return false;
            Dirty();
            return true;
        }

        void IExamine.Examine(FormattedMessage message, bool inDetailsRange)
        {
            if (Activated)
            {
                message.AddMarkup(Loc.GetString("The light is currently [color=darkgreen]on[/color]."));
            }
            else
            {
                message.AddMarkup(Loc.GetString("The light is currently [color=darkred]off[/color]."));
            }
        }

        bool IUse.UseEntity(UseEntityEventArgs eventArgs)
        {
            return ToggleStatus(eventArgs.User);
        }

        /// <summary>
        ///     Illuminates the light if it is not active, extinguishes it if it is active.
        /// </summary>
        /// <returns>True if the light's status was toggled, false otherwise.</returns>
        private bool ToggleStatus(IEntity user)
        {
            return Activated ? TurnOff() : TurnOn(user);
        }

        private bool TurnOff(bool makeNoise = true)
        {
            if (!Activated)
            {
                return false;
            }

            SetState(false);
            Activated = false;

            if (makeNoise)
            {
                if (TurnOffSound != null) EntitySystem.Get<AudioSystem>().PlayFromEntity(TurnOffSound, Owner);
            }

            return true;
        }

        private bool TurnOn(IEntity user)
        {
            if (Activated)
            {
                return false;
            }

            if (Cell == null)
            {
                if (TurnOnFailSound != null) EntitySystem.Get<AudioSystem>().PlayFromEntity(TurnOnFailSound, Owner);
                Owner.PopupMessage(user, Loc.GetString("Cell missing..."));
                return false;
            }

            // To prevent having to worry about frame time in here.
            // Let's just say you need a whole second of charge before you can turn it on.
            // Simple enough.
            if (Wattage > Cell.CurrentCharge)
            {
                if (TurnOnFailSound != null) EntitySystem.Get<AudioSystem>().PlayFromEntity(TurnOnFailSound, Owner);
                Owner.PopupMessage(user, Loc.GetString("Dead cell..."));
                return false;
            }

            Activated = true;
            SetState(true);

            if (TurnOnSound != null) EntitySystem.Get<AudioSystem>().PlayFromEntity(TurnOnSound, Owner);
            return true;
        }

        private void SetState(bool on)
        {
            if (Owner.TryGetComponent(out SpriteComponent? sprite))
            {
                sprite.LayerSetVisible(1, on);
            }

            if (Owner.TryGetComponent(out PointLightComponent? light))
            {
                light.Enabled = on;
            }

            if (Owner.TryGetComponent(out ClothingComponent? clothing))
            {
                clothing.ClothingEquippedPrefix = on ? "On" : "Off";
            }

            if (Owner.TryGetComponent(out ItemComponent? item))
            {
                item.EquippedPrefix = on ? "on" : "off";
            }
        }

        public void OnUpdate(float frameTime)
        {
            if (Cell == null)
            {
                TurnOff(false);
                return;
            }

            var appearanceComponent = Owner.GetComponent<AppearanceComponent>();

            if (Cell.MaxCharge - Cell.CurrentCharge < Cell.MaxCharge * 0.70)
            {
                appearanceComponent.SetData(HandheldLightVisuals.Power, HandheldLightPowerStates.FullPower);
            }
            else if (Cell.MaxCharge - Cell.CurrentCharge < Cell.MaxCharge * 0.90)
            {
                appearanceComponent.SetData(HandheldLightVisuals.Power, HandheldLightPowerStates.LowPower);
            }
            else
            {
                appearanceComponent.SetData(HandheldLightVisuals.Power, HandheldLightPowerStates.Dying);
            }

            if (Activated && !Cell.TryUseCharge(Wattage * frameTime)) TurnOff(false);
            Dirty();
        }

        public override ComponentState GetComponentState()
        {
            if (Cell == null)
            {
                return new HandheldLightComponentState(null, false);
            }

            if (Wattage > Cell.CurrentCharge)
            {
                // Practically zero.
                // This is so the item status works correctly.
                return new HandheldLightComponentState(0, HasCell);
            }

            return new HandheldLightComponentState(Cell.CurrentCharge / Cell.MaxCharge, HasCell);
        }
    }
}
