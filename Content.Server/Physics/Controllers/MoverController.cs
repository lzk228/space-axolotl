using Content.Server.Cargo.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Physics.Controllers
{
    public sealed class MoverController : SharedMoverController
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly ShuttleSystem _shuttle = default!;
        [Dependency] private readonly ThrusterSystem _thruster = default!;

        private Dictionary<ShuttleComponent, List<(PilotComponent, InputMoverComponent, TransformComponent)>> _shuttlePilots = new();

        protected override Filter GetSoundPlayers(EntityUid mover)
        {
            return Filter.Pvs(mover, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == mover);
        }

        protected override bool CanSound()
        {
            return true;
        }

        public override void UpdateBeforeSolve(bool prediction, float frameTime)
        {
            base.UpdateBeforeSolve(prediction, frameTime);

            var bodyQuery = GetEntityQuery<PhysicsComponent>();
            var relayQuery = GetEntityQuery<RelayInputMoverComponent>();

            foreach (var (mover, xform) in EntityQuery<InputMoverComponent, TransformComponent>())
            {
                if (relayQuery.TryGetComponent(mover.Owner, out var relayed) && relayed != null)
                {
                    continue;
                }

                PhysicsComponent? body = null;

                if (mover.ToParent && relayQuery.HasComponent(xform.ParentUid))
                {
                    if (!bodyQuery.TryGetComponent(xform.ParentUid, out body)) continue;
                }
                else if (!bodyQuery.TryGetComponent(mover.Owner, out body))
                {
                    continue;
                }

                HandleMobMovement(mover, body, xform, frameTime);
            }

            HandleShuttleMovement(frameTime);
        }

        public (Vector2 Strafe, float Rotation, float Brakes) GetPilotVelocityInput(PilotComponent component)
        {
            if (!Timing.InSimulation)
            {
                // Outside of simulation we'll be running client predicted movement per-frame.
                // So return a full-length vector as if it's a full tick.
                // Physics system will have the correct time step anyways.
                ResetSubtick(component);
                ApplyTick(component, 1f);
                return (component.CurTickStrafeMovement, component.CurTickRotationMovement, component.CurTickBraking);
            }

            float remainingFraction;

            if (Timing.CurTick > component.LastInputTick)
            {
                component.CurTickStrafeMovement = Vector2.Zero;
                component.CurTickRotationMovement = 0f;
                component.CurTickBraking = 0f;
                remainingFraction = 1;
            }
            else
            {
                remainingFraction = (ushort.MaxValue - component.LastInputSubTick) / (float) ushort.MaxValue;
            }

            ApplyTick(component, remainingFraction);

            // Logger.Info($"{curDir}{walk}{sprint}");
            return (component.CurTickStrafeMovement, component.CurTickRotationMovement, component.CurTickBraking);
        }

        private void ResetSubtick(PilotComponent component)
        {
            if (Timing.CurTick <= component.LastInputTick) return;

            component.CurTickStrafeMovement = Vector2.Zero;
            component.CurTickRotationMovement = 0f;
            component.CurTickBraking = 0f;
            component.LastInputTick = Timing.CurTick;
            component.LastInputSubTick = 0;
        }

        protected override void HandleShuttleInput(EntityUid uid, ShuttleButtons button, ushort subTick, bool state)
        {
            if (!TryComp<PilotComponent>(uid, out var pilot) || pilot.Console == null) return;

            ResetSubtick(pilot);

            if (subTick >= pilot.LastInputSubTick)
            {
                var fraction = (subTick - pilot.LastInputSubTick) / (float) ushort.MaxValue;

                ApplyTick(pilot, fraction);
                pilot.LastInputSubTick = subTick;
            }

            var buttons = pilot.HeldButtons;

            if (state)
            {
                buttons |= button;
            }
            else
            {
                buttons &= ~button;
            }

            pilot.HeldButtons = buttons;
        }

        private void ApplyTick(PilotComponent component, float fraction)
        {
            var x = 0;
            var y = 0;
            var rot = 0;
            int brake;

            if ((component.HeldButtons & ShuttleButtons.StrafeLeft) != 0x0)
            {
                x -= 1;
            }

            if ((component.HeldButtons & ShuttleButtons.StrafeRight) != 0x0)
            {
                x += 1;
            }

            component.CurTickStrafeMovement.X += x * fraction;

            if ((component.HeldButtons & ShuttleButtons.StrafeUp) != 0x0)
            {
                y += 1;
            }

            if ((component.HeldButtons & ShuttleButtons.StrafeDown) != 0x0)
            {
                y -= 1;
            }

            component.CurTickStrafeMovement.Y += y * fraction;

            if ((component.HeldButtons & ShuttleButtons.RotateLeft) != 0x0)
            {
                rot -= 1;
            }

            if ((component.HeldButtons & ShuttleButtons.RotateRight) != 0x0)
            {
                rot += 1;
            }

            component.CurTickRotationMovement += rot * fraction;

            if ((component.HeldButtons & ShuttleButtons.Brake) != 0x0)
            {
                brake = 1;
            }
            else
            {
                brake = 0;
            }

            component.CurTickBraking += brake * fraction;
        }

        private void HandleShuttleMovement(float frameTime)
        {
            var newPilots = new Dictionary<ShuttleComponent, List<(PilotComponent Pilot, InputMoverComponent Mover, TransformComponent ConsoleXform)>>();

            // We just mark off their movement and the shuttle itself does its own movement
            foreach (var (pilot, mover) in EntityManager.EntityQuery<PilotComponent, InputMoverComponent>())
            {
                var consoleEnt = pilot.Console?.Owner;

                // TODO: This is terrible. Just make a new mover and also make it remote piloting + device networks
                if (TryComp<CargoPilotConsoleComponent>(consoleEnt, out var cargoConsole))
                {
                    consoleEnt = cargoConsole.Entity;
                }

                if (!TryComp<TransformComponent>(consoleEnt, out var xform)) continue;

                var gridId = xform.GridUid;
                // This tries to see if the grid is a shuttle and if the console should work.
                if (!_mapManager.TryGetGrid(gridId, out var grid) ||
                    !EntityManager.TryGetComponent(grid.GridEntityId, out ShuttleComponent? shuttleComponent) ||
                    !shuttleComponent.Enabled) continue;

                if (!newPilots.TryGetValue(shuttleComponent, out var pilots))
                {
                    pilots = new List<(PilotComponent, InputMoverComponent, TransformComponent)>();
                    newPilots[shuttleComponent] = pilots;
                }

                pilots.Add((pilot, mover, xform));
            }

            // Reset inputs for non-piloted shuttles.
            foreach (var (shuttle, _) in _shuttlePilots)
            {
                if (newPilots.ContainsKey(shuttle)) continue;

                _thruster.DisableLinearThrusters(shuttle);
            }

            _shuttlePilots = newPilots;

            // Collate all of the linear / angular velocites for a shuttle
            // then do the movement input once for it.
            foreach (var (shuttle, pilots) in _shuttlePilots)
            {
                if (Paused(shuttle.Owner) || !TryComp(shuttle.Owner, out PhysicsComponent? body)) continue;

                // Collate movement linear and angular inputs together
                var linearInput = Vector2.Zero;
                var angularInput = 0f;

                foreach (var (pilot, _, consoleXform) in pilots)
                {
                    var pilotInput = GetPilotVelocityInput(pilot);

                    // On the one hand we could just make it relay inputs to brake
                    // but uhh may be disorienting? n
                    if (pilotInput.Brakes > 0f)
                    {
                        if (body.LinearVelocity.Length > 0f)
                        {
                            var force = body.LinearVelocity.Normalized * pilotInput.Brakes / body.InvMass * 3f;
                            var impulse = force * body.InvMass * frameTime;

                            if (impulse.Length > body.LinearVelocity.Length)
                            {
                                body.LinearVelocity = Vector2.Zero;
                            }
                            else
                            {
                                body.ApplyLinearImpulse(-force * frameTime);
                            }
                        }

                        if (body.AngularVelocity != 0f)
                        {
                            var force = body.AngularVelocity * pilotInput.Brakes / body.InvI * 2f;
                            var impulse = force * body.InvI * frameTime;

                            if (MathF.Abs(impulse) > MathF.Abs(body.AngularVelocity))
                            {
                                body.AngularVelocity = 0f;
                            }
                            else
                            {
                                body.ApplyAngularImpulse(-force * frameTime);
                            }
                        }

                        continue;
                    }

                    if (pilotInput.Strafe.Length > 0f)
                    {
                        var offsetRotation = consoleXform.LocalRotation;
                        linearInput += offsetRotation.RotateVec(pilotInput.Strafe);
                    }

                    if (pilotInput.Rotation != 0f)
                    {
                        angularInput += pilotInput.Rotation;
                    }
                }

                var count = pilots.Count;
                linearInput /= count;
                angularInput /= count;

                // Handle shuttle movement
                if (linearInput.Length.Equals(0f))
                {
                    _thruster.DisableLinearThrusters(shuttle);
                    body.LinearDamping = _shuttle.ShuttleIdleLinearDamping * body.InvMass;
                    if (body.LinearVelocity.Length < 0.08)
                    {
                        body.LinearVelocity = Vector2.Zero;
                    }
                }
                else
                {
                    body.LinearDamping = 0;
                    var angle = linearInput.ToWorldAngle();
                    var linearDir = angle.GetDir();
                    var dockFlag = linearDir.AsFlag();
                    var shuttleNorth = EntityManager.GetComponent<TransformComponent>(body.Owner).WorldRotation.ToWorldVec();

                    var totalForce = new Vector2();

                    // Won't just do cardinal directions.
                    foreach (DirectionFlag dir in Enum.GetValues(typeof(DirectionFlag)))
                    {
                        // Brain no worky but I just want cardinals
                        switch (dir)
                        {
                            case DirectionFlag.South:
                            case DirectionFlag.East:
                            case DirectionFlag.North:
                            case DirectionFlag.West:
                                break;
                            default:
                                continue;
                        }

                        if ((dir & dockFlag) == 0x0)
                        {
                            _thruster.DisableLinearThrustDirection(shuttle, dir);
                            continue;
                        }

                        float length;
                        Angle thrustAngle;

                        switch (dir)
                        {
                            case DirectionFlag.North:
                                length = linearInput.Y;
                                thrustAngle = new Angle(MathF.PI);
                                break;
                            case DirectionFlag.South:
                                length = -linearInput.Y;
                                thrustAngle = new Angle(0f);
                                break;
                            case DirectionFlag.East:
                                length = linearInput.X;
                                thrustAngle = new Angle(MathF.PI / 2f);
                                break;
                            case DirectionFlag.West:
                                length = -linearInput.X;
                                thrustAngle = new Angle(-MathF.PI / 2f);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        _thruster.EnableLinearThrustDirection(shuttle, dir);

                        var index = (int) Math.Log2((int) dir);
                        var force = thrustAngle.RotateVec(shuttleNorth) * shuttle.LinearThrust[index] * length;

                        totalForce += force;
                    }

                    body.ApplyLinearImpulse(totalForce * frameTime);
                }

                if (MathHelper.CloseTo(angularInput, 0f))
                {
                    _thruster.SetAngularThrust(shuttle, false);
                    body.AngularDamping = _shuttle.ShuttleIdleAngularDamping * body.InvI;
                    body.SleepingAllowed = true;

                    if (Math.Abs(body.AngularVelocity) < 0.01f)
                    {
                        body.AngularVelocity = 0f;
                    }
                }
                else
                {
                    body.AngularDamping = 0;
                    body.SleepingAllowed = false;

                    var maxSpeed = Math.Min(_shuttle.ShuttleMaxAngularMomentum * body.InvI, _shuttle.ShuttleMaxAngularSpeed);
                    var maxTorque = body.Inertia * _shuttle.ShuttleMaxAngularAcc;

                    var torque = Math.Min(shuttle.AngularThrust, maxTorque);
                    var dragTorque = body.AngularVelocity * (torque / maxSpeed);

                    body.ApplyAngularImpulse((-angularInput * torque - dragTorque) * frameTime);

                    _thruster.SetAngularThrust(shuttle, true);
                }
            }
        }
    }
}
