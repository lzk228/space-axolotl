using Content.Server.Botany.Components;
using Content.Shared.Atmos;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
///     changes the gases that a plant or produce create.
/// </summary>
public sealed partial class PlantMutateExudeGasses : EntityEffect
{
    [DataField]
    public float MinValue = 0.01f;

    [DataField]
    public float MaxValue = 0.5f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var gasses = args.EntityManager.GetComponent<ExudeGasGrowthComponent>(args.TargetEntity);

        if (gasses == null)
            return;

        var random = IoCManager.Resolve<IRobustRandom>();

        // Add a random amount of a random gas to this gas dictionary
        float amount = random.NextFloat(MinValue, MaxValue);
        Gas gas = random.Pick(Enum.GetValues(typeof(Gas)).Cast<Gas>().ToList());
        if (gasses.ExudeGasses.ContainsKey(gas))
        {
            gasses.ExudeGasses[gas] += amount;
        }
        else
        {
            gasses.ExudeGasses.Add(gas, amount);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "TODO";
    }
}

/// <summary>
///     changes the gases that a plant or produce consumes.
/// </summary>
public sealed partial class PlantMutateConsumeGasses : EntityEffect
{
    [DataField]
    public float MinValue = 0.01f;

    [DataField]
    public float MaxValue = 0.5f;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var gasses = args.EntityManager.GetComponent<ConsumeGasGrowthComponent>(args.TargetEntity);
        if (gasses == null)
            return;

        var random = IoCManager.Resolve<IRobustRandom>();

        // Add a random amount of a random gas to this gas dictionary
        float amount = random.NextFloat(MinValue, MaxValue);
        Gas gas = random.Pick(Enum.GetValues(typeof(Gas)).Cast<Gas>().ToList());
        if (gasses.ConsumeGasses.ContainsKey(gas))
        {
            gasses.ConsumeGasses[gas] += amount;
        }
        else
        {
            gasses.ConsumeGasses.Add(gas, amount);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "TODO";
    }
}
