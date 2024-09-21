using Content.Server.Botany.Components;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;

namespace Content.Server.EntityEffects.Effects.PlantMetabolism;

[UsedImplicitly]
public sealed partial class PlantAdjustToxins : PlantAdjustAttribute
{
    public override string GuidebookAttributeName { get; set; } = "plant-attribute-toxins";
    public override bool GuidebookIsAttributePositive { get; protected set; } = false;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var plantComp = args.EntityManager.GetComponent<PlantComponent>(args.TargetEntity);
        if (plantComp.PlantHolderUid == null)
            return;
        var plantHolderComp = args.EntityManager.GetComponent<PlantHolderComponent>(plantComp.PlantHolderUid.Value);
        plantHolderComp.Toxins += Amount;
    }
}

