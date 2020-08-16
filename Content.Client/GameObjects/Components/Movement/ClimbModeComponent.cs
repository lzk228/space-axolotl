﻿using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Content.Shared.GameObjects.Components.Movement;
using Content.Shared.Physics;

namespace Content.Client.GameObjects.Components.Movement
{
    [RegisterComponent]
    public class ClimbModeComponent : SharedClimbModeComponent
    {
        public override void Initialize()
        {
            base.Initialize();

            Owner.TryGetComponent(out ICollidableComponent body);
            _body = body;
        }

        private ICollidableComponent _body = default;

        public override void HandleComponentState(ComponentState curState, ComponentState nextState)
        {
            if (!(curState is ClimbModeComponentState climbModeState) || _body == null)
            {
                return;
            }

            if (climbModeState.Climbing)
            {
                _body.PhysicsShapes[0].CollisionMask &= ~((int) CollisionGroup.VaultImpassable);
            }
            else
            {
                _body.PhysicsShapes[0].CollisionMask |= ((int) CollisionGroup.VaultImpassable);
            }           
        }
    }
}
