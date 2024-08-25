using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Server.StationEvents.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using JetBrains.Annotations;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Chemistry.EntitySystems;

namespace Content.Server.StationEvents.Events;

[UsedImplicitly]
public sealed class VentClogRule : StationEventSystem<VentClogRuleComponent>
{
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly ChemistryRegistrySystem _chemRegistry = default!;

    protected override void Started(
        EntityUid uid,
        VentClogRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        // TODO: "safe random" for chems. Right now this includes admin chemicals.
        var allReagents = _chemRegistry.EnumerateReagents()
            .Select(x => x.Comp.Id).ToList();

        foreach (var (_, transform) in EntityManager.EntityQuery<GasVentPumpComponent, TransformComponent>())
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station != chosenStation)
            {
                continue;
            }

            var solution = new Solution();

            if (!RobustRandom.Prob(0.33f))
                continue;

            var pickAny = RobustRandom.Prob(0.05f);
            var reagent = RobustRandom.Pick(pickAny ? allReagents : component.SafeishVentChemicals);

            var weak = component.WeakReagents.Contains(reagent);
            var quantity = weak ? component.WeakReagentQuantity : component.ReagentQuantity;
            solution.AddReagent(reagent, quantity);

            var foamEnt = Spawn("Foam", transform.Coordinates);
            var spreadAmount = weak ? component.WeakSpread : component.Spread;
            _smoke.StartSmoke(foamEnt, solution, component.Time, spreadAmount);
            Audio.PlayPvs(component.Sound, transform.Coordinates);
        }
    }
}
