using System.Collections.Generic;
using System.Linq;
using Content.Server.Body.Circulatory;
using Content.Server.Body.Mechanism;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Mechanism;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server.Body.Metabolism
{
    // TODO mirror in the future working on mechanisms move updating here to BodySystem so it can be ordered?
    [UsedImplicitly]
    public class MetabolizerSystem : EntitySystem
    {
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MetabolizerComponent, ComponentInit>(OnMetabolizerInit);
        }

        private void OnMetabolizerInit(EntityUid uid, MetabolizerComponent component, ComponentInit args)
        {
            if (!component.SolutionOnBody)
                _solutionContainerSystem.EnsureSolution(uid, component.SolutionName);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var metab in EntityManager.EntityQuery<MetabolizerComponent>(false))
            {
                metab.AccumulatedFrametime += frameTime;

                // Only update as frequently as it should
                if (metab.AccumulatedFrametime >= metab.UpdateFrequency)
                {
                    metab.AccumulatedFrametime -= metab.UpdateFrequency;
                    TryMetabolize(metab.OwnerUid, metab);
                }
            }
        }

        private void TryMetabolize(EntityUid uid, MetabolizerComponent? meta=null, MechanismComponent? mech=null)
        {
            if (!Resolve(uid, ref meta))
                return;

            Resolve(uid, ref mech, false);

            // First step is get the solution we actually care about
            Solution? solution = null;
            EntityUid? solutionEntityUid = null;

            if (meta.SolutionOnBody)
            {
                if (mech != null)
                {
                    var body = mech.Body;

                    if (body != null)
                    {
                        _solutionContainerSystem.TryGetSolution(body.OwnerUid, meta.SolutionName, out solution);
                        solutionEntityUid = body.OwnerUid;
                    }
                }
            }
            else
            {
                _solutionContainerSystem.TryGetSolution(uid, meta.SolutionName, out solution);
                solutionEntityUid = uid;
            }

            if (solutionEntityUid == null || solution == null)
                return;
            // we found our guy
            foreach (var reagent in solution.Contents)
            {
                if (!_prototypeManager.TryIndex<ReagentPrototype>(reagent.ReagentId, out var proto))
                    continue;

                if (proto.Metabolisms == null)
                    continue;

                // loop over all our groups and see which ones apply
                FixedPoint2 mostToRemove = FixedPoint2.Zero;
                foreach (var group in meta.MetabolismGroups)
                {
                    if (!proto.Metabolisms.Keys.Contains(group.Id))
                        continue;

                    var entry = proto.Metabolisms[group.Id];

                    // we don't remove reagent for every group, just whichever had the biggest rate
                    if (entry.MetabolismRate > mostToRemove)
                        mostToRemove = entry.MetabolismRate;

                    // do all effects, if conditions apply
                    foreach (var effect in entry.Effects)
                    {
                        bool failed = false;
                        var ent = EntityManager.GetEntity(solutionEntityUid.Value);
                        var quant = new Solution.ReagentQuantity(reagent.ReagentId, reagent.Quantity);
                        if (effect.Conditions != null)
                        {
                            foreach (var cond in effect.Conditions)
                            {
                                if (!cond.Condition(ent, meta.Owner, quant))
                                    failed = true;
                            }

                            if (failed)
                                continue;
                        }

                        effect.Metabolize(ent, quant);
                    }
                }

                // remove a certain amount of reagent
                if (mostToRemove > FixedPoint2.Zero)
                    _solutionContainerSystem.TryRemoveReagent(solutionEntityUid.Value, null, reagent.ReagentId, mostToRemove);
            }
        }
    }
}
