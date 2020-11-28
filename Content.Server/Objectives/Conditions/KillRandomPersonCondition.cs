﻿using System.Linq;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.Mobs;
using Content.Server.Objectives.Interfaces;
using Content.Shared.GameObjects.Components.Damage;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server.Objectives.Conditions
{
    [UsedImplicitly]
    public class KillRandomPersonCondition : KillPersonCondition
    {
        public KillRandomPersonCondition()
        {
            var entityMgr = IoCManager.Resolve<IEntityManager>();
            var allHumans = entityMgr.ComponentManager.EntityQuery<MindComponent>().Where(mc =>
            {
                var entity = mc.Mind?.OwnedEntity;
                return entity != null &&
                       entity.TryGetComponent<IDamageableComponent>(out var damageableComponent) &&
                       damageableComponent.CurrentState == DamageState.Alive;
            }).Select(mc => mc.Mind).ToList();
            Target = IoCManager.Resolve<IRobustRandom>().Pick(allHumans);
        }
    }
}
