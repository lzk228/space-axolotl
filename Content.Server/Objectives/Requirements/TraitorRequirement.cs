﻿using Content.Server.Mind;
using Content.Server.Objectives.Interfaces;
using Content.Server.Roles;
using JetBrains.Annotations;

namespace Content.Server.Objectives.Requirements
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class TraitorRequirement : IObjectiveRequirement
    {
        public bool CanBeAssigned(EntityUid mindId, MindComponent mind)
        {
            var mindSystem = IoCManager.Resolve<IEntityManager>().System<MindSystem>();
            return mindSystem.HasRole<TraitorRoleComponent>(mindId);
        }
    }
}
