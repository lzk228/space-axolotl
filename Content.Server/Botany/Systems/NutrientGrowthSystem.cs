using Content.Server.Botany.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Botany.Systems
{
    public sealed class NutrientGrowthSystem : PlantGrowthSystem
    {
        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Update(float frameTime)
        {
            if (nextUpdate > _gameTiming.CurTime)
                return;

            var query = EntityQueryEnumerator<NutrientGrowthComponent>();
            while (query.MoveNext(out var uid, out var nutrientGrowthComponent))
            {
                Update(uid, nutrientGrowthComponent);
            }
            nextUpdate = _gameTiming.CurTime + updateDelay;
        }

        public void Update(EntityUid uid, NutrientGrowthComponent component)
        {
            PlantHolderComponent? holder = null;
            Resolve<PlantHolderComponent>(uid, ref holder);

            if (holder == null || holder.Seed == null || holder.Dead)
                return;

            if (component.NutrientConsumption > 0 && holder.NutritionLevel > 0 && _random.Prob(0.75f))
            {
                holder.NutritionLevel -= MathF.Max(0f,
                    component.NutrientConsumption * HydroponicsConsumptionMultiplier * HydroponicsSpeedMultiplier);
                if (holder.DrawWarnings)
                    holder.UpdateSpriteAfterUpdate = true;
            }

            var healthMod = _random.Next(1, 3) * HydroponicsSpeedMultiplier;
            if (holder.SkipAging < 10)
            {
                // Make sure the plant is not hungry
                if (holder.NutritionLevel > 5)
                {
                    holder.Health += Convert.ToInt32(_random.Prob(0.35f)) * healthMod;
                }
                else
                {
                    AffectGrowth(-1, holder);
                    holder.Health -= healthMod;
                }
            }
        }
    }
}
