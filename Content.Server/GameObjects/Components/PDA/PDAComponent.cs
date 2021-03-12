#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Access;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Server.GameObjects.Components.Paper;
using Content.Server.Interfaces;
using Content.Server.Interfaces.GameObjects.Components.Items;
using Content.Server.Interfaces.PDA;
using Content.Server.Utility;
using Content.Shared.GameObjects.Components.PDA;
using Content.Shared.GameObjects.Components.Tag;
using Content.Shared.GameObjects.EntitySystems.ActionBlocker;
using Content.Shared.GameObjects.Verbs;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using Content.Shared.Access;

namespace Content.Server.GameObjects.Components.PDA
{
    [RegisterComponent]
    [ComponentReference(typeof(IActivate))]
    [ComponentReference(typeof(IAccess))]
    public class PDAComponent : SharedPDAComponent, IInteractUsing, IActivate, IUse, IAccess, IMapInit
    {
        [Dependency] private readonly IPDAUplinkManager _uplinkManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        [ViewVariables] private ContainerSlot _idSlot = default!;
        [ViewVariables] private ContainerSlot _penSlot = default!;

        [ViewVariables] private bool _lightOn;

        [ViewVariables] [DataField("idCard")] private string? _startingIdCard = "AssistantIDCard";
        [ViewVariables] [DataField("pen")] private string? _startingPen = "Pen";

        [ViewVariables] public string? OwnerName { get; private set; }

        [ViewVariables] public IdCardComponent? ContainedID { get; private set; }
        [ViewVariables] public bool IdSlotEmpty => _idSlot.ContainedEntity == null;
        [ViewVariables] public bool PenSlotEmpty => _penSlot.ContainedEntity == null;

        private UplinkAccount? _syndicateUplinkAccount;

        [ViewVariables] public UplinkAccount? SyndicateUplinkAccount => _syndicateUplinkAccount;

        [ViewVariables] private readonly AccessTags _accessSet;

        [ViewVariables] private BoundUserInterface? UserInterface => Owner.GetUIOrNull(PDAUiKey.Key);

        public PDAComponent()
        {
            _accessSet = 0;
        }

        public override void Initialize()
        {
            base.Initialize();
            _idSlot = ContainerHelpers.EnsureContainer<ContainerSlot>(Owner, "pda_entity_container");
            _penSlot = ContainerHelpers.EnsureContainer<ContainerSlot>(Owner, "pda_pen_slot");

            if (UserInterface != null)
            {
                UserInterface.OnReceiveMessage += UserInterfaceOnReceiveMessage;
            }

            UpdatePDAAppearance();
        }

        public void MapInit()
        {
            if (!string.IsNullOrEmpty(_startingIdCard))
            {
                var idCard = _entityManager.SpawnEntity(_startingIdCard, Owner.Transform.Coordinates);
                var idCardComponent = idCard.GetComponent<IdCardComponent>();
                _idSlot.Insert(idCardComponent.Owner);
                ContainedID = idCardComponent;
            }

            if (!string.IsNullOrEmpty(_startingPen))
            {
                var pen = _entityManager.SpawnEntity(_startingPen, Owner.Transform.Coordinates);
                _penSlot.Insert(pen);
            }
        }

        private void UserInterfaceOnReceiveMessage(ServerBoundUserInterfaceMessage message)
        {
            switch (message.Message)
            {
                case PDARequestUpdateInterfaceMessage _:
                {
                    UpdatePDAUserInterface();
                    break;
                }
                case PDAToggleFlashlightMessage _:
                {
                    ToggleLight();
                    break;
                }

                case PDAEjectIDMessage _:
                {
                    HandleIDEjection(message.Session.AttachedEntity!);
                    break;
                }

                case PDAEjectPenMessage _:
                {
                    HandlePenEjection(message.Session.AttachedEntity!);
                    break;
                }

                case PDAUplinkBuyListingMessage buyMsg:
                {
                    if (message.Session.AttachedEntity == null)
                        break;

                    if (!_uplinkManager.TryPurchaseItem(_syndicateUplinkAccount, buyMsg.ItemId,
                        message.Session.AttachedEntity.Transform.Coordinates, out var entity))
                    {
                        SendNetworkMessage(new PDAUplinkInsufficientFundsMessage(), message.Session.ConnectedClient);
                        break;
                    }

                    HandsComponent.PutInHandOrDropStatic(
                        message.Session.AttachedEntity,
                        entity.GetComponent<ItemComponent>());

                    SendNetworkMessage(new PDAUplinkBuySuccessMessage(), message.Session.ConnectedClient);
                    break;
                }
            }
        }

        private void UpdatePDAUserInterface()
        {
            var ownerInfo = new PDAIdInfoText
            {
                ActualOwnerName = OwnerName,
                IdOwner = ContainedID?.FullName,
                JobTitle = ContainedID?.JobTitle
            };

            //Do we have an account? If so provide the info.
            if (_syndicateUplinkAccount != null)
            {
                var accData = new UplinkAccountData(_syndicateUplinkAccount.AccountHolder,
                    _syndicateUplinkAccount.Balance);
                var listings = _uplinkManager.FetchListings.Values.ToArray();
                UserInterface?.SetState(new PDAUpdateState(_lightOn, !PenSlotEmpty, ownerInfo, accData, listings));
            }
            else
            {
                UserInterface?.SetState(new PDAUpdateState(_lightOn, !PenSlotEmpty, ownerInfo));
            }

            UpdatePDAAppearance();
        }

        private void UpdatePDAAppearance()
        {
            if (Owner.TryGetComponent(out AppearanceComponent? appearance))
            {
                appearance.SetData(PDAVisuals.FlashlightLit, _lightOn);
                appearance.SetData(PDAVisuals.IDCardInserted, !IdSlotEmpty);
            }
        }

        private bool TryInsertIdCard(InteractUsingEventArgs eventArgs, IdCardComponent idCardComponent)
        {
            var item = eventArgs.Using;
            if (_idSlot.Contains(item))
                return false;

            if (!eventArgs.User.TryGetComponent(out IHandsComponent? hands))
            {
                Owner.PopupMessage(eventArgs.User, Loc.GetString("You have no hands!"));
                return true;
            }

            IEntity? swap = null;
            if (!IdSlotEmpty)
            {
                // Swap
                swap = _idSlot.ContainedEntities[0];
            }

            if (!hands.Drop(item))
            {
                return true;
            }

            if (swap != null)
            {
                hands.PutInHand(swap.GetComponent<ItemComponent>());
            }

            InsertIdCard(idCardComponent);

            UpdatePDAUserInterface();
            return true;
        }

        private bool TryInsertPen(InteractUsingEventArgs eventArgs)
        {
            var item = eventArgs.Using;
            if (_penSlot.Contains(item))
                return false;

            if (!eventArgs.User.TryGetComponent(out IHandsComponent? hands))
            {
                Owner.PopupMessage(eventArgs.User, Loc.GetString("You have no hands!"));
                return true;
            }

            IEntity? swap = null;
            if (!PenSlotEmpty)
            {
                // Swap
                swap = _penSlot.ContainedEntities[0];
            }

            if (!hands.Drop(item))
            {
                return true;
            }

            if (swap != null)
            {
                hands.PutInHand(swap.GetComponent<ItemComponent>());
            }

            // Insert Pen
            _penSlot.Insert(item);

            UpdatePDAUserInterface();
            return true;
        }

        async Task<bool> IInteractUsing.InteractUsing(InteractUsingEventArgs eventArgs)
        {
            var item = eventArgs.Using;

            if (item.TryGetComponent<IdCardComponent>(out var idCardComponent))
            {
                return TryInsertIdCard(eventArgs, idCardComponent);
            }

            if (item.HasTag("Write"))
            {
                return TryInsertPen(eventArgs);
            }

            return false;
        }

        void IActivate.Activate(ActivateEventArgs eventArgs)
        {
            if (!eventArgs.User.TryGetComponent(out IActorComponent? actor))
            {
                return;
            }

            UserInterface?.Toggle(actor.playerSession);
            UpdatePDAAppearance();
        }

        bool IUse.UseEntity(UseEntityEventArgs eventArgs)
        {
            if (!eventArgs.User.TryGetComponent(out IActorComponent? actor))
            {
                return false;
            }

            UserInterface?.Toggle(actor.playerSession);
            UpdatePDAAppearance();
            return true;
        }

        public void SetPDAOwner(string name)
        {
            OwnerName = name;
            UpdatePDAUserInterface();
        }

        public void InsertIdCard(IdCardComponent card)
        {
            _idSlot.Insert(card.Owner);
            ContainedID = card;
            EntitySystem.Get<AudioSystem>().PlayFromEntity("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg", Owner);
        }

        /// <summary>
        /// Initialize the PDA's syndicate uplink account.
        /// </summary>
        /// <param name="acc"></param>
        public void InitUplinkAccount(UplinkAccount acc)
        {
            _syndicateUplinkAccount = acc;
            _uplinkManager.AddNewAccount(_syndicateUplinkAccount);

            _syndicateUplinkAccount.BalanceChanged += account =>
            {
                UpdatePDAUserInterface();
            };

            UpdatePDAUserInterface();
        }

        private void ToggleLight()
        {
            if (!Owner.TryGetComponent(out PointLightComponent? light))
            {
                return;
            }

            _lightOn = !_lightOn;
            light.Enabled = _lightOn;
            EntitySystem.Get<AudioSystem>().PlayFromEntity("/Audio/Items/flashlight_toggle.ogg", Owner);
            UpdatePDAUserInterface();
        }

        private void HandleIDEjection(IEntity pdaUser)
        {
            if (ContainedID == null)
            {
                return;
            }

            var cardEntity = ContainedID.Owner;
            _idSlot.Remove(cardEntity);

            var hands = pdaUser.GetComponent<HandsComponent>();
            var cardItemComponent = cardEntity.GetComponent<ItemComponent>();
            hands.PutInHandOrDrop(cardItemComponent);
            ContainedID = null;

            EntitySystem.Get<AudioSystem>().PlayFromEntity("/Audio/Machines/id_swipe.ogg", Owner);
            UpdatePDAUserInterface();
        }

        private void HandlePenEjection(IEntity pdaUser)
        {
            if (PenSlotEmpty)
                return;

            var pen = _penSlot.ContainedEntities[0];
            _penSlot.Remove(pen);

            var hands = pdaUser.GetComponent<HandsComponent>();
            var itemComponent = pen.GetComponent<ItemComponent>();
            hands.PutInHandOrDrop(itemComponent);

            UpdatePDAUserInterface();
        }

        [Verb]
        public sealed class EjectIDVerb : Verb<PDAComponent>
        {
            protected override void GetData(IEntity user, PDAComponent component, VerbData data)
            {
                if (!ActionBlockerSystem.CanInteract(user))
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Text = Loc.GetString("Eject ID");
                data.Visibility = component.IdSlotEmpty ? VerbVisibility.Invisible : VerbVisibility.Visible;
            }

            protected override void Activate(IEntity user, PDAComponent component)
            {
                component.HandleIDEjection(user);
            }
        }

        [Verb]
        public sealed class EjectPenVerb : Verb<PDAComponent>
        {
            protected override void GetData(IEntity user, PDAComponent component, VerbData data)
            {
                if (!ActionBlockerSystem.CanInteract(user))
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Text = Loc.GetString("Eject Pen");
                data.Visibility = component.PenSlotEmpty ? VerbVisibility.Invisible : VerbVisibility.Visible;
            }

            protected override void Activate(IEntity user, PDAComponent component)
            {
                component.HandlePenEjection(user);
            }
        }

        [Verb]
        public sealed class ToggleFlashlightVerb : Verb<PDAComponent>
        {
            protected override void GetData(IEntity user, PDAComponent component, VerbData data)
            {
                if (!ActionBlockerSystem.CanInteract(user))
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Text = Loc.GetString("Toggle flashlight");
            }

            protected override void Activate(IEntity user, PDAComponent component)
            {
                component.ToggleLight();
            }
        }

        private AccessTags? GetContainedAccess()
        {
            return ContainedID?.Owner?.GetComponent<AccessComponent>()?.Tags;
        }

        AccessTags IAccess.Tags => _accessSet;

        bool IAccess.IsReadOnly => true;

        void IAccess.SetTags(AccessTags newTags)
        {
            throw new NotSupportedException("PDA access list is read-only.");
        }
    }
}
