﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Power.ApcNetComponents;
using Content.Server.Interfaces;
using Content.Server.Interfaces.GameObjects;
using Content.Server.Interfaces.GameObjects.Components.Items;
using Content.Shared.GameObjects;
using Content.Shared.GameObjects.Components.Disposal;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.Components.Container;
using Robust.Server.GameObjects.Components.UserInterface;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using Timer = Robust.Shared.Timers.Timer;

namespace Content.Server.GameObjects.Components.Disposal
{
    [RegisterComponent]
    [ComponentReference(typeof(IInteractUsing))]
    public class DisposalUnitComponent : SharedDisposalUnitComponent, IInteractHand, IInteractUsing
    {
#pragma warning disable 649
        [Dependency] private readonly IServerNotifyManager _notifyManager = default!;
#pragma warning restore 649

        public override string Name => "DisposalUnit";

        /// <summary>
        ///     The delay for an entity trying to move out of this unit.
        /// </summary>
        private static readonly TimeSpan ExitAttemptDelay = TimeSpan.FromSeconds(0.5);

        /// <summary>
        ///     Last time that an entity tried to exit this disposal unit.
        /// </summary>
        private TimeSpan _lastExitAttempt;

        /// <summary>
        ///     The current pressure of this disposal unit.
        ///     Prevents it from flushing if it is not equal to or bigger than 1.
        /// </summary>
        private float _pressure;

        private bool _engaged;

        [ViewVariables]
        private TimeSpan _engageTime;

        [ViewVariables]
        private TimeSpan _automaticEngageTime;

        /// <summary>
        ///     Token used to cancel the automatic engage of a disposal unit
        ///     after an entity enters it.
        /// </summary>
        private CancellationTokenSource? _automaticEngageToken;

        /// <summary>
        ///     Container of entities inside this disposal unit.
        /// </summary>
        [ViewVariables]
        private Container _container = default!;

        [ViewVariables] public IReadOnlyList<IEntity> ContainedEntities => _container.ContainedEntities;

        [ViewVariables]
        private BoundUserInterface _userInterface = default!;

        [ViewVariables]
        public bool Powered =>
            !Owner.TryGetComponent(out PowerReceiverComponent receiver) ||
            receiver.Powered;

        [ViewVariables]
        public bool Anchored =>
            !Owner.TryGetComponent(out CollidableComponent collidable) ||
            collidable.Anchored;

        [ViewVariables]
        private State State => _pressure >= 1 ? State.Ready : State.Pressurizing;

        [ViewVariables]
        public bool Engaged
        {
            get => _engaged;
            set
            {
                _engaged = value;
                UpdateVisualState();
            }
        }

        public bool CanInsert(IEntity entity)
        {
            return Powered &&
                   Anchored &&
                   entity.HasComponent<DisposableComponent>() &&
                   _container.CanInsert(entity);
        }

        public bool TryInsert(IEntity entity)
        {
            if (!CanInsert(entity) || !_container.Insert(entity))
            {
                return false;
            }

            _automaticEngageToken = new CancellationTokenSource();

            Timer.Spawn(_automaticEngageTime, () => TryFlush(), _automaticEngageToken.Token);

            if (entity.TryGetComponent(out IActorComponent actor))
            {
                _userInterface.Close(actor.playerSession);
            }

            return true;
        }

        private void Remove(IEntity entity)
        {
            _container.Remove(entity);

            if (ContainedEntities.Count == 0)
            {
                _automaticEngageToken?.Cancel();
                _automaticEngageToken = null;
            }
        }

        private bool CanFlush()
        {
            return _engaged &&
                   _pressure >= 1 &&
                   (!Owner.TryGetComponent(out PowerReceiverComponent receiver) ||
                    receiver.Powered) &&
                   (!Owner.TryGetComponent(out CollidableComponent collidable) ||
                    collidable.Anchored);
        }

        public bool TryFlush()
        {
            if (!CanFlush())
            {
                return false;
            }

            var snapGrid = Owner.GetComponent<SnapGridComponent>();
            var entry = snapGrid
                .GetLocal()
                .FirstOrDefault(entity => entity.HasComponent<DisposalEntryComponent>());

            if (entry == null)
            {
                return false;
            }

            var entryComponent = entry.GetComponent<DisposalEntryComponent>();
            foreach (var entity in _container.ContainedEntities.ToList())
            {
                _container.Remove(entity);
                entryComponent.TryInsert(entity);
            }

            _automaticEngageToken?.Cancel();
            _automaticEngageToken = null;

            _pressure = 0;

            UpdateVisualState(true);
            UpdateInterface();

            _engaged = false;

            return true;
        }

        private void TryEject(IEntity entity)
        {
            _container.Remove(entity);
        }

        private void TryEjectContents()
        {
            foreach (var entity in _container.ContainedEntities.ToArray())
            {
                TryEject(entity);
            }
        }

        private void TogglePower()
        {
            if (!Owner.TryGetComponent(out PowerReceiverComponent receiver))
            {
                return;
            }

            receiver.PowerDisabled = !receiver.PowerDisabled;
            UpdateInterface();
        }

        private DisposalUnitBoundUserInterfaceState GetInterfaceState()
        {
            return new DisposalUnitBoundUserInterfaceState(Owner.Name, Loc.GetString($"{State}"), _pressure, Powered);
        }

        private void UpdateInterface()
        {
            var state = GetInterfaceState();
            _userInterface.SetState(state);
        }

        private bool PlayerCanUse(IEntity player)
        {
            if (player == null)
            {
                return false;
            }

            if (!ActionBlockerSystem.CanInteract(player) ||
                !ActionBlockerSystem.CanUse(player))
            {
                return false;
            }

            return true;
        }

        private void OnUiReceiveMessage(ServerBoundUserInterfaceMessage obj)
        {
            if (obj.Session.AttachedEntity == null)
            {
                return;
            }

            if (!PlayerCanUse(obj.Session.AttachedEntity))
            {
                return;
            }

            if (!(obj.Message is UiButtonPressedMessage message))
            {
                return;
            }

            switch (message.Button)
            {
                case UiButton.Eject:
                    TryEjectContents();
                    break;
                case UiButton.Engage:
                    _engaged = true;
                    TryFlush();
                    break;
                case UiButton.Power:
                    TogglePower();

                    EntitySystem.Get<AudioSystem>().PlayFromEntity("/Audio/Machines/machine_switch.ogg", Owner, AudioParams.Default.WithVolume(-2f));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateVisualState()
        {
            UpdateVisualState(false);
        }

        private void UpdateVisualState(bool flush)
        {
            if (!Owner.TryGetComponent(out AppearanceComponent appearance))
            {
                return;
            }

            appearance.SetData(Visuals.Handle, _engaged
                ? HandleState.Engaged
                : HandleState.Normal);

            if (!Anchored)
            {
                appearance.SetData(Visuals.VisualState, VisualState.UnAnchored);
                return;
            }

            if (flush)
            {
                appearance.SetData(Visuals.VisualState, VisualState.Flushing);
            }
            else
            {
                appearance.SetData(Visuals.VisualState, _pressure < 1
                    ? VisualState.Charging
                    : VisualState.Ready);
            }
        }

        public void Update(float frameTime)
        {
            if (!Powered)
            {
                return;
            }

            var oldPressure = _pressure;

            _pressure = _pressure + frameTime > 1
                ? 1
                : _pressure + 0.05f * frameTime;

            if (oldPressure < 1 && _pressure >= 1)
            {
                UpdateVisualState();

                if (_engaged)
                {
                    TryFlush();
                }
            }

            UpdateInterface();
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataReadWriteFunction(
                "pressure",
                1.0f,
                pressure => _pressure = pressure,
                () => _pressure);

            serializer.DataReadWriteFunction(
                "engageTime",
                2,
                seconds => _engageTime = TimeSpan.FromSeconds(seconds),
                () => (int) _engageTime.TotalSeconds);

            serializer.DataReadWriteFunction(
                "automaticEngageTime",
                30,
                seconds => _automaticEngageTime = TimeSpan.FromSeconds(seconds),
                () => (int) _automaticEngageTime.TotalSeconds);
        }

        public override void Initialize()
        {
            base.Initialize();

            _container = ContainerManagerComponent.Ensure<Container>(Name, Owner);
            _userInterface = Owner.GetComponent<ServerUserInterfaceComponent>()
                .GetBoundUserInterface(DisposalUnitUiKey.Key);
            _userInterface.OnReceiveMessage += OnUiReceiveMessage;

            UpdateInterface();
        }

        protected override void Startup()
        {
            base.Startup();

            Owner.EnsureComponent<AnchorableComponent>();
            var collidable = Owner.EnsureComponent<CollidableComponent>();

            collidable.AnchoredChanged += UpdateVisualState;
            UpdateVisualState();
        }

        public override void OnRemove()
        {
            foreach (var entity in _container.ContainedEntities.ToArray())
            {
                _container.ForceRemove(entity);
            }

            _userInterface.CloseAll();

            _automaticEngageToken?.Cancel();
            _automaticEngageToken = null;

            _container = null!;

            base.OnRemove();
        }

        public override void HandleMessage(ComponentMessage message, IComponent? component)
        {
            base.HandleMessage(message, component);

            switch (message)
            {
                case RelayMovementEntityMessage msg:
                    var timing = IoCManager.Resolve<IGameTiming>();
                    if (_engaged ||
                        !msg.Entity.HasComponent<HandsComponent>() ||
                        timing.CurTime < _lastExitAttempt + ExitAttemptDelay)
                    {
                        break;
                    }

                    _lastExitAttempt = timing.CurTime;
                    Remove(msg.Entity);
                    break;
            }
        }

        bool IInteractHand.InteractHand(InteractHandEventArgs eventArgs)
        {
            if (!ActionBlockerSystem.CanInteract(eventArgs.User))
            {
                _notifyManager.PopupMessage(Owner.Transform.GridPosition, eventArgs.User,
                    Loc.GetString("You can't do that!"));
                return false;
            }

            if (ContainerHelpers.IsInContainer(eventArgs.User))
            {
                _notifyManager.PopupMessage(Owner.Transform.GridPosition, eventArgs.User,
                    Loc.GetString("You can't reach there!"));
                return false;
            }

            if (!eventArgs.User.TryGetComponent(out IActorComponent actor))
            {
                return false;
            }

            if (!eventArgs.User.HasComponent<IHandsComponent>())
            {
                _notifyManager.PopupMessage(Owner.Transform.GridPosition, eventArgs.User,
                    Loc.GetString("You have no hands!"));
                return false;
            }

            _userInterface.Open(actor.playerSession);
            return true;
        }

        bool IInteractUsing.InteractUsing(InteractUsingEventArgs eventArgs)
        {
            return TryInsert(eventArgs.Using);
        }

        [Verb]
        private sealed class SelfInsertVerb : Verb<DisposalUnitComponent>
        {
            protected override void GetData(IEntity user, DisposalUnitComponent component, VerbData data)
            {
                data.Visibility = VerbVisibility.Invisible;

                if (!ActionBlockerSystem.CanInteract(user))
                {
                    return;
                }

                data.Visibility = VerbVisibility.Visible;
                data.Text = Loc.GetString("Jump inside");
            }

            protected override void Activate(IEntity user, DisposalUnitComponent component)
            {
                component.TryInsert(user);
            }
        }

        [Verb]
        private sealed class FlushVerb : Verb<DisposalUnitComponent>
        {
            protected override void GetData(IEntity user, DisposalUnitComponent component, VerbData data)
            {
                data.Visibility = VerbVisibility.Invisible;

                if (!ActionBlockerSystem.CanInteract(user))
                {
                    return;
                }

                data.Visibility = VerbVisibility.Visible;
                data.Text = Loc.GetString("Flush");
            }

            protected override void Activate(IEntity user, DisposalUnitComponent component)
            {
                component._engaged = true;
                component.TryFlush();
            }
        }
    }
}
