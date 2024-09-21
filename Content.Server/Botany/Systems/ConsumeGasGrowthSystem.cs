using Content.Server.Atmos.EntitySystems;
using Content.Server.Botany.Components;
using Content.Shared.Atmos;

namespace Content.Server.Botany.Systems
{
    public sealed class ConsumeGasGrowthSystem : PlantGrowthSystem
    {
        [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Update(float frameTime)
        {
            if (nextUpdate > _gameTiming.CurTime)
                return;

            var query = EntityQueryEnumerator<ConsumeGasGrowthComponent>();
            while (query.MoveNext(out var uid, out var consumeGasGrowthComponent))
            {
                Update(uid, consumeGasGrowthComponent);
            }
            nextUpdate = _gameTiming.CurTime + updateDelay;
        }

        public void Update(EntityUid uid, ConsumeGasGrowthComponent component)
        {
            PlantHolderComponent? holder = null;
            Resolve<PlantHolderComponent>(uid, ref holder);

            if (holder == null || holder.Seed == null || holder.Dead)
                return;

            var environment = _atmosphere.GetContainingMixture(uid, true, true) ?? GasMixture.SpaceGas;

            holder.MissingGas = 0;
            if (component.ConsumeGasses.Count > 0)
            {
                foreach (var (gas, amount) in component.ConsumeGasses)
                {
                    if (environment.GetMoles(gas) < amount)
                    {
                        holder.MissingGas++;
                        continue;
                    }

                    environment.AdjustMoles(gas, -amount);
                }

                if (holder.MissingGas > 0)
                {
                    holder.Health -= holder.MissingGas * HydroponicsSpeedMultiplier;
                    if (holder.DrawWarnings)
                        holder.UpdateSpriteAfterUpdate = true;
                }
            }
        }
    }
}
