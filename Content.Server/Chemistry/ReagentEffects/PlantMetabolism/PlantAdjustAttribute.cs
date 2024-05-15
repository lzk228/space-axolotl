﻿using Content.Server.Botany.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server.Chemistry.ReagentEffects.PlantMetabolism
{
    [ImplicitDataDefinitionForInheritors]
    public abstract partial class PlantAdjustAttribute : ReagentEffect
    {
        [DataField]
        public float Amount { get; protected set; } = 1;

        [DataField]
        public float Prob { get; protected set; } = 1; // = (80);

        /// <summary>
        /// Localisation key for the name of the adjusted attribute. Used for guidebook description.
        /// </summary>
        [DataField]
        public string Attribute { get; protected set; } = "missing";

        /// <summary>
        /// Whether the attribute in question is a good thing. Used for guidebook description to determine the color of the number.
        /// </summary>
        [DataField]
        public bool Positive = true;

        /// <summary>
        ///     Checks if the plant holder can metabolize the reagent or not. Checks if it has an alive plant by default.
        /// </summary>
        /// <param name="plantHolder">The entity holding the plant</param>
        /// <param name="plantHolderComponent">The plant holder component</param>
        /// <param name="entityManager">The entity manager</param>
        /// <param name="mustHaveAlivePlant">Whether to check if it has an alive plant or not</param>
        /// <returns></returns>
        public bool CanMetabolize(EntityUid plantHolder, [NotNullWhen(true)] out PlantHolderComponent? plantHolderComponent,
            IEntityManager entityManager,
            bool mustHaveAlivePlant = true)
        {
            plantHolderComponent = null;

            if (!entityManager.TryGetComponent(plantHolder, out plantHolderComponent)
                                    || mustHaveAlivePlant && (plantHolderComponent.Seed == null || plantHolderComponent.Dead))
                return false;

            if (Prob >= 1f)
                return true;

            // Dependencies are never injected for reagents if you intend to do that for this.
            return !(Prob <= 0f) && IoCManager.Resolve<IRobustRandom>().Prob(Prob);
        }

        protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        {
            string color;
            if (Positive ^ Amount < 0.0)
            {
                color = "green";
            }
            else
            {
                color = "red";
            }
            return Loc.GetString("reagent-effect-guidebook-plant-attribute", ("attribute", Loc.GetString(Attribute)), ("amount", Amount.ToString("0.00")), ("colorvalue", color), ("chance", Probability));
        }
    }
}
