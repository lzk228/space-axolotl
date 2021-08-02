using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Solution;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;

namespace Content.Server.Chemistry.ReagentEffects
{
    /// <summary>
    /// Default metabolism for medicine reagents. Attempts to find a DamageableComponent on the target,
    /// and to update its damage values.
    /// </summary>
    public class HealthChange : ReagentEffect
    {
        /// <summary>
        /// How much damage is changed when 1u of the reagent is metabolized.
        /// </summary>
        [DataField("healthChange")]
        public float AmountToChange { get; set; } = 1.0f;

        /// <summary>
        /// Damage group to change.
        /// </summary>
        // TODO PROTOTYPE Replace this datafield variable with prototype references, once they are supported.
        // Also requires replacing DamageGroup() calls with something like _damageGroup
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [DataField("damageGroup", required: true)]
        private readonly string _damageGroupID = default!;

        private float _accumulatedHealth;

        /// <summary>
        ///     Changes damage if a DamageableComponent can be found.
        /// </summary>
        public override void Metabolize(IEntity solutionEntity, Solution.ReagentQuantity amount)
        {
            if (solutionEntity.TryGetComponent(out IDamageableComponent? damageComponent))
            {
                var damageGroup = IoCManager.Resolve<IPrototypeManager>().Index<DamageGroupPrototype>(_damageGroupID);

                damageComponent.TryChangeDamage(damageGroup, (int)AmountToChange, true);

                float decHealthChange = (float) (AmountToChange - (int) AmountToChange);
                _accumulatedHealth += decHealthChange;

                if (_accumulatedHealth >= 1)
                {
                    damageComponent.TryChangeDamage(damageGroup, 1, true);
                    _accumulatedHealth -= 1;
                }

                else if(_accumulatedHealth <= -1)
                {
                    damageComponent.TryChangeDamage(damageGroup, -1, true);
                    _accumulatedHealth += 1;
                }
            }
        }
    }
}
