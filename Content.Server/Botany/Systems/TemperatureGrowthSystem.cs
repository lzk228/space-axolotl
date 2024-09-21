using Content.Server.Atmos.EntitySystems;
using Content.Server.Botany.Components;
using Content.Shared.Atmos;

namespace Content.Server.Botany.Systems
{
    public sealed class TemperatureGrowthSystem : PlantGrowthSystem
    {
        [Dependency] private readonly BotanySystem _botany = default!;
        [Dependency] private readonly PlantHolderSystem _plantHolderSystem = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Update(float frameTime)
        {
            if (nextUpdate > _gameTiming.CurTime)
                return;

            var query = EntityQueryEnumerator<TemperatureGrowthComponent>();
            while (query.MoveNext(out var uid, out var TemperatureGrowthComponent))
            {
                Update(uid, TemperatureGrowthComponent);
            }
            nextUpdate = _gameTiming.CurTime + updateDelay;
        }

        public void Update(EntityUid uid, TemperatureGrowthComponent component)
        {
            PlantHolderComponent? holder = null;
            Resolve<PlantHolderComponent>(uid, ref holder);

            if (holder == null || holder.Seed == null || holder.Dead)
                return;

            var environment = _atmosphere.GetContainingMixture(uid, true, true) ?? GasMixture.SpaceGas;
            if (MathF.Abs(environment.Temperature - component.IdealHeat) > component.HeatTolerance)
            {
                holder.Health -= _random.Next(1, 3);
                holder.ImproperHeat = true;
                if (holder.DrawWarnings)
                    holder.UpdateSpriteAfterUpdate = true;
            }
            else
            {
                holder.ImproperHeat = false;
            }
        }
    }
}
