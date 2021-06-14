﻿#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.UserInterface;
using Content.Shared.Body.Components;
using Content.Shared.Body.Mechanism;
using Content.Shared.Body.Part;
using Content.Shared.Body.Surgery;
using Content.Shared.Interaction;
using Content.Shared.Notification;
using Content.Shared.Notification.Managers;
using Content.Shared.Random.Helpers;
using Content.Shared.Verbs;
using Robust.Server.Console;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.ViewVariables;

namespace Content.Server.Body.Part
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedBodyPartComponent))]
    [ComponentReference(typeof(IBodyPart))]
    public class BodyPartComponent : SharedBodyPartComponent, IAfterInteract
    {
        private readonly Dictionary<int, object> _optionsCache = new();
        private IBody? _owningBodyCache;
        private int _idHash;
        private IEntity? _surgeonCache;
        private Container _mechanismContainer = default!;

        [ViewVariables] private BoundUserInterface? UserInterface => Owner.GetUIOrNull(SurgeryUIKey.Key);

        public override bool CanAddMechanism(IMechanism mechanism)
        {
            return base.CanAddMechanism(mechanism) &&
                   _mechanismContainer.CanInsert(mechanism.Owner);
        }

        protected override void OnAddMechanism(IMechanism mechanism)
        {
            base.OnAddMechanism(mechanism);

            _mechanismContainer.Insert(mechanism.Owner);
        }

        protected override void OnRemoveMechanism(IMechanism mechanism)
        {
            base.OnRemoveMechanism(mechanism);

            _mechanismContainer.Remove(mechanism.Owner);
            mechanism.Owner.RandomOffset(0.25f);
        }

        public override void Initialize()
        {
            base.Initialize();

            _mechanismContainer = Owner.EnsureContainer<Container>($"{Name}-{nameof(BodyPartComponent)}");

            // This is ran in Startup as entities spawned in Initialize
            // are not synced to the client since they are assumed to be
            // identical on it
            foreach (var mechanismId in MechanismIds)
            {
                var entity = Owner.EntityManager.SpawnEntity(mechanismId, Owner.Transform.MapPosition);

                if (!entity.TryGetComponent(out IMechanism? mechanism))
                {
                    Logger.Error($"Entity {mechanismId} does not have a {nameof(IMechanism)} component.");
                    continue;
                }

                TryAddMechanism(mechanism, true);
            }
        }

        protected override void Startup()
        {
            base.Startup();

            if (UserInterface != null)
            {
                UserInterface.OnReceiveMessage += OnUIMessage;
            }

            foreach (var mechanism in Mechanisms)
            {
                mechanism.Dirty();
            }
        }

        async Task<bool> IAfterInteract.AfterInteract(AfterInteractEventArgs eventArgs)
        {
            // TODO BODY
            if (eventArgs.Target == null)
            {
                return false;
            }

            CloseAllSurgeryUIs();
            _optionsCache.Clear();
            _surgeonCache = null;
            _owningBodyCache = null;

            if (eventArgs.Target.TryGetComponent(out IBody? body))
            {
                SendSlots(eventArgs, body);
            }

            return true;
        }

        private void SendSlots(AfterInteractEventArgs eventArgs, IBody body)
        {
            // Create dictionary to send to client (text to be shown : data sent back if selected)
            var toSend = new Dictionary<string, int>();

            // Here we are trying to grab a list of all empty BodySlots adjacent to an existing BodyPart that can be
            // attached to. i.e. an empty left hand slot, connected to an occupied left arm slot would be valid.
            foreach (var slot in body.EmptySlots)
            {
                if (slot.PartType != PartType)
                {
                    continue;
                }

                foreach (var connection in slot.Connections)
                {
                    if (connection.Part == null ||
                        !connection.Part.CanAttachPart(this))
                    {
                        continue;
                    }

                    _optionsCache.Add(_idHash, slot);
                    toSend.Add(slot.Id, _idHash++);
                }
            }

            if (_optionsCache.Count > 0)
            {
                OpenSurgeryUI(eventArgs.User.GetComponent<ActorComponent>().PlayerSession);
                BodyPartSlotRequest(eventArgs.User.GetComponent<ActorComponent>().PlayerSession,
                    toSend);
                _surgeonCache = eventArgs.User;
                _owningBodyCache = body;
            }
            else // If surgery cannot be performed, show message saying so.
            {
                eventArgs.Target?.PopupMessage(eventArgs.User,
                    Loc.GetString("You see no way to install {0:theName}.", Owner));
            }
        }

        /// <summary>
        ///     Called after the client chooses from a list of possible
        ///     BodyPartSlots to install the limb on.
        /// </summary>
        private void ReceiveBodyPartSlot(int key)
        {
            if (_surgeonCache == null ||
                !_surgeonCache.TryGetComponent(out ActorComponent? actor))
            {
                return;
            }

            CloseSurgeryUI(actor.PlayerSession);

            if (_owningBodyCache == null)
            {
                return;
            }

            // TODO: sanity checks to see whether user is in range, user is still able-bodied, target is still the same, etc etc
            if (!_optionsCache.TryGetValue(key, out var targetObject))
            {
                _owningBodyCache.Owner.PopupMessage(_surgeonCache,
                    Loc.GetString("You see no useful way to attach {0:theName} anymore.", Owner));
            }

            var target = (string) targetObject!;
            var message = _owningBodyCache.TryAddPart(target, this)
                ? Loc.GetString("You attach {0:theName}.", Owner)
                : Loc.GetString("You can't attach {0:theName}!", Owner);

            _owningBodyCache.Owner.PopupMessage(_surgeonCache, message);
        }

        private void OpenSurgeryUI(IPlayerSession session)
        {
            UserInterface?.Open(session);
        }

        private void BodyPartSlotRequest(IPlayerSession session, Dictionary<string, int> options)
        {
            UserInterface?.SendMessage(new RequestBodyPartSlotSurgeryUIMessage(options), session);
        }

        private void CloseSurgeryUI(IPlayerSession session)
        {
            UserInterface?.Close(session);
        }

        private void CloseAllSurgeryUIs()
        {
            UserInterface?.CloseAll();
        }

        private void OnUIMessage(ServerBoundUserInterfaceMessage message)
        {
            switch (message.Message)
            {
                case ReceiveBodyPartSlotSurgeryUIMessage msg:
                    ReceiveBodyPartSlot(msg.SelectedOptionId);
                    break;
            }
        }

        [Verb]
        public class AttachBodyPartVerb : Verb<BodyPartComponent>
        {
            protected override void GetData(IEntity user, BodyPartComponent component, VerbData data)
            {
                data.Visibility = VerbVisibility.Invisible;

                if (user == component.Owner)
                {
                    return;
                }

                if (!user.TryGetComponent(out ActorComponent? actor))
                {
                    return;
                }

                var groupController = IoCManager.Resolve<IConGroupController>();

                if (!groupController.CanCommand(actor.PlayerSession, "attachbodypart"))
                {
                    return;
                }

                if (!user.TryGetComponent(out IBody? body))
                {
                    return;
                }

                if (body.HasPart(component))
                {
                    return;
                }

                data.Visibility = VerbVisibility.Visible;
                data.Text = Loc.GetString("Attach Body Part");
            }

            protected override void Activate(IEntity user, BodyPartComponent component)
            {
                if (!user.TryGetComponent(out IBody? body))
                {
                    return;
                }

                body.SetPart($"{nameof(AttachBodyPartVerb)}-{component.Owner.Uid}", component);
            }
        }
    }
}
