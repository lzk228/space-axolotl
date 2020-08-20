﻿#nullable enable
using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Damage;
using Content.Server.GameObjects.Components.Interactable;
using Content.Server.GameObjects.Components.Power.ApcNetComponents;
using Content.Server.GameObjects.EntitySystems;
using Content.Server.Interfaces;
using Content.Shared.GameObjects.Components.Gravity;
using Content.Shared.GameObjects.Components.Interactable;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.Components.UserInterface;
using Robust.Server.Interfaces.GameObjects;
using Robust.Server.Interfaces.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;

namespace Content.Server.GameObjects.Components.Gravity
{
    [RegisterComponent]
    public class GravityGeneratorComponent : SharedGravityGeneratorComponent, IInteractUsing, IBreakAct, IInteractHand
    {

        private bool _switchedOn;

        private bool _intact;

        private GravityGeneratorStatus _status;

        public bool Powered => PowerReceiver == null || PowerReceiver.Powered;

        public bool SwitchedOn => _switchedOn;

        public bool Intact => _intact;

        public GravityGeneratorStatus Status => _status;

        public bool NeedsUpdate
        {
            get
            {
                switch (_status)
                {
                    case GravityGeneratorStatus.On:
                        return !(Powered && SwitchedOn && Intact);
                    case GravityGeneratorStatus.Off:
                        return SwitchedOn || !(Powered && Intact);
                    case GravityGeneratorStatus.Unpowered:
                        return SwitchedOn || Powered || !Intact;
                    case GravityGeneratorStatus.Broken:
                        return SwitchedOn || Powered || Intact;
                    default:
                        return true; // This _should_ be unreachable
                }
            }
        }

        public override string Name => "GravityGenerator";

        private BoundUserInterface? UserInterface =>
            Owner.TryGetComponent(out ServerUserInterfaceComponent ui) &&
            ui.TryGetBoundUserInterface(GravityGeneratorUiKey.Key, out var boundUi)
                ? boundUi
                : null;

        private PowerReceiverComponent? PowerReceiver =>
            Owner.TryGetComponent(out PowerReceiverComponent receiver) ? receiver : null;

        private SpriteComponent? Sprite => Owner.TryGetComponent(out SpriteComponent sprite) ? sprite : null;

        public override void Initialize()
        {
            base.Initialize();

            if (UserInterface != null)
            {
                UserInterface.OnReceiveMessage += HandleUIMessage;
            }

            _switchedOn = true;
            _intact = true;
            _status = GravityGeneratorStatus.On;
            UpdateState();
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _switchedOn, "switched_on", true);
            serializer.DataField(ref _intact, "intact", true);
        }

        bool IInteractHand.InteractHand(InteractHandEventArgs eventArgs)
        {
            if (!eventArgs.User.TryGetComponent<IActorComponent>(out var actor))
                return false;
            if (Status != GravityGeneratorStatus.Off && Status != GravityGeneratorStatus.On)
            {
                return false;
            }
            OpenUserInterface(actor.playerSession);
            return true;
        }

        public async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (!eventArgs.Using.TryGetComponent(out WelderComponent tool))
                return false;

            if (!await tool.UseTool(eventArgs.User, Owner, 2f, ToolQuality.Welding, 5f))
                return false;

            // Repair generator
            var breakable = Owner.GetComponent<BreakableComponent>();
            breakable.FixAllDamage();
            _intact = true;

            var notifyManager = IoCManager.Resolve<IServerNotifyManager>();

            notifyManager.PopupMessage(Owner, eventArgs.User, Loc.GetString("You repair {0:theName} with {1:theName}", Owner, eventArgs.Using));

            return true;
        }

        public void OnBreak(BreakageEventArgs eventArgs)
        {
            _intact = false;
            _switchedOn = false;
        }

        public void UpdateState()
        {
            if (!Intact)
            {
                MakeBroken();
            }
            else if (!Powered)
            {
                MakeUnpowered();
            }
            else if (!SwitchedOn)
            {
                MakeOff();
            }
            else
            {
                MakeOn();
            }
        }

        private void HandleUIMessage(ServerBoundUserInterfaceMessage message)
        {
            switch (message.Message)
            {
                case GeneratorStatusRequestMessage _:
                    UserInterface?.SetState(new GeneratorState(Status == GravityGeneratorStatus.On));
                    break;
                case SwitchGeneratorMessage msg:
                    _switchedOn = msg.On;
                    UpdateState();
                    break;
                default:
                    break;
            }
        }

        private void OpenUserInterface(IPlayerSession playerSession)
        {
            UserInterface?.Open(playerSession);
        }

        private void MakeBroken()
        {
            _status = GravityGeneratorStatus.Broken;
            Sprite?.LayerSetState(0, "broken");
            Sprite?.LayerSetVisible(1, false);
        }

        private void MakeUnpowered()
        {
            _status = GravityGeneratorStatus.Unpowered;
            Sprite?.LayerSetState(0, "off");
            Sprite?.LayerSetVisible(1, false);
        }

        private void MakeOff()
        {
            _status = GravityGeneratorStatus.Off;
            Sprite?.LayerSetState(0, "off");
            Sprite?.LayerSetVisible(1, false);
        }

        private void MakeOn()
        {
            _status = GravityGeneratorStatus.On;
            Sprite?.LayerSetState(0, "on");
            Sprite?.LayerSetVisible(1, true);
        }
    }

    public enum GravityGeneratorStatus
    {
        Broken,
        Unpowered,
        Off,
        On
    }
}
