using Content.Shared.Drone;
using Content.Server.Drone.Components;
using Content.Shared.MobState;
using Content.Shared.MobState.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction.Components;
using Content.Shared.Examine;
using Content.Server.Popups;
using Content.Server.Mind.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Hands.Components;
using Content.Shared.Body.Components;
using Content.Server.UserInterface;
using Content.Shared.Emoting;
using Robust.Shared.Player;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Item;
using Content.Shared.DragDrop;

namespace Content.Server.Drone
{
    public sealed class DroneSystem : SharedDroneSystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DroneComponent, InteractionAttemptEvent>(OnInteractionAttempt);
            SubscribeLocalEvent<DroneComponent, UserOpenActivatableUIAttemptEvent>(OnActivateUIAttempt);
            SubscribeLocalEvent<DroneComponent, MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<DroneComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<DroneComponent, MindAddedMessage>(OnMindAdded);
            SubscribeLocalEvent<DroneComponent, MindRemovedMessage>(OnMindRemoved);
            SubscribeLocalEvent<DroneComponent, EmoteAttemptEvent>(OnEmoteAttempt);
            SubscribeLocalEvent<DroneComponent, ThrowAttemptEvent>(OnThrowAttempt);
            SubscribeLocalEvent<DroneComponent, DropAttemptEvent>(OnDropAttempt);
        }

        private void OnInteractionAttempt(EntityUid uid, DroneComponent component, InteractionAttemptEvent args)
        {
            if (OrganicsInRange(uid, component))
                args.Cancel();

            if (HasComp<SharedItemComponent>(args.Target) && !HasComp<UnremoveableComponent>(args.Target))
            {
                if (!_tagSystem.HasTag(args.Target.Value, "DroneUsable") && !_tagSystem.HasTag(args.Target.Value, "Trash"))
                    args.Cancel();
            }
        }

        private void OnActivateUIAttempt(EntityUid uid, DroneComponent component, UserOpenActivatableUIAttemptEvent args)
        {
            args.Cancel();
        }

        private void OnExamined(EntityUid uid, DroneComponent component, ExaminedEvent args)
        {
            if (args.IsInDetailsRange)
            {
                if (TryComp<MindComponent>(uid, out var mind) && mind.HasMind)
                {
                    args.PushMarkup(Loc.GetString("drone-active"));
                }
                else
                {
                    args.PushMarkup(Loc.GetString("drone-dormant"));
                }
            }
        }

        private void OnMobStateChanged(EntityUid uid, DroneComponent drone, MobStateChangedEvent args)
        {
            if (args.Component.IsDead())
            {
                var body = Comp<SharedBodyComponent>(uid); //There's no way something can have a mobstate but not a body...

                foreach (var item in drone.ToolUids)
                {
                    EntityManager.DeleteEntity(item);
                }
                body.Gib();
                EntityManager.DeleteEntity(uid);
            }
        }

        private void OnMindAdded(EntityUid uid, DroneComponent drone, MindAddedMessage args)
        {
            UpdateDroneAppearance(uid, DroneStatus.On);
            _popupSystem.PopupEntity(Loc.GetString("drone-activated"), uid, Filter.Pvs(uid));

            if (drone.AlreadyAwoken == false)
            {
                var spawnCoord = Transform(uid).Coordinates;

                if (drone.Tools.Count == 0) return;

                if (TryComp<HandsComponent>(uid, out var hands) && hands.Count >= drone.Tools.Count)
                {
                   foreach (var entry in drone.Tools)
                    {
                        var item = EntityManager.SpawnEntity(entry.PrototypeId, spawnCoord);
                        AddComp<UnremoveableComponent>(item);
                        hands.PutInHand(item);
                        drone.ToolUids.Add(item);
                    }
                }

                drone.AlreadyAwoken = true;
            }
        }

        private void OnMindRemoved(EntityUid uid, DroneComponent drone, MindRemovedMessage args)
        {
            UpdateDroneAppearance(uid, DroneStatus.Off);
            EnsureComp<GhostTakeoverAvailableComponent>(uid);
        }

        private void OnEmoteAttempt(EntityUid uid, DroneComponent component, EmoteAttemptEvent args)
        {
            // No.
            args.Cancel();
        }

        private void OnThrowAttempt(EntityUid uid, DroneComponent drone, ThrowAttemptEvent args)
        {
            args.Cancel();
        }

        private void OnDropAttempt(EntityUid uid, DroneComponent drone, DropAttemptEvent args)
        {
            if (OrganicsInRange(uid, drone))
                args.Cancel();
        }

        private void UpdateDroneAppearance(EntityUid uid, DroneStatus status)
        {
            if (TryComp<AppearanceComponent>(uid, out var appearance))
            {
                appearance.SetData(DroneVisuals.Status, status);
            }
        }

        private bool OrganicsInRange(EntityUid uid, DroneComponent component)
        {
            var xform = Comp<TransformComponent>(uid);
            foreach (var entity in _lookup.GetEntitiesInRange(xform.MapID, xform.WorldPosition, component.InteractionBlockRange))
            {
                if (HasComp<MobStateComponent>(entity) && !HasComp<DroneComponent>(entity))
                {
                    _popupSystem.PopupEntity(Loc.GetString("drone-too-close"), uid, Filter.Entities(uid));
                    return true;
                }
            }
            return false;
        }
    }
}
