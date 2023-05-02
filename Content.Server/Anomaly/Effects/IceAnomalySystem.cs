using Content.Server.Weapons.Ranged.Systems;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Interaction;
using Content.Shared.Anomaly.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Anomaly.Effects.Components;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Anomaly.Effects;

/// <summary>
/// This handles <see cref="IceAnomalyComponent"/> and the events from <seealso cref="AnomalySystem"/>
/// </summary>
public sealed class IceAnomalySystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ExplosionSystem _boom = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<IceAnomalyComponent, AnomalyPulseEvent>(OnPulse);
        SubscribeLocalEvent<IceAnomalyComponent, AnomalySupercriticalEvent>(OnSupercritical);
    }

    private void OnPulse(EntityUid uid, IceAnomalyComponent component, ref AnomalyPulseEvent args)
    {
        ShootProjectilesAtEntites(uid, component, args.Severity, args.Stability);
    }

    private void ShootProjectilesAtEntites(EntityUid uid, IceAnomalyComponent component, float severity, float stability)
    {
        var xform = Transform(uid);
        var projectilesShot = 0;
        var range = Math.Abs(component.ProjectileRange * stability); // Apparently this shit can be a negative somehow?

        foreach (var entity in _lookup.GetEntitiesInRange(uid, range, LookupFlags.Dynamic))
        {
            if (projectilesShot >= component.MaxProjectiles * severity)
                return;

            // Living entities are more likely to be shot at then non living
            if (!HasComp<MobStateComponent>(entity) && _random.Prob(0.5f))
                continue;

            var targetCoords = Transform(entity).Coordinates.Offset(_random.NextVector2(-1, 1));

            ShootProjectile(
                uid, component,
                xform.Coordinates,
                targetCoords,
                severity
            );
            projectilesShot++;
        }
    }

    private void ShootProjectile(
        EntityUid uid,
        IceAnomalyComponent component,
        EntityCoordinates coords,
        EntityCoordinates targetCoords,
        float severity
        )
    {
        var mapPos = coords.ToMap(EntityManager, _xform);

        var spawnCoords = _mapManager.TryFindGridAt(mapPos, out var grid)
                ? coords.WithEntityId(grid.Owner, EntityManager)
                : new(_mapManager.GetMapEntityId(mapPos.MapId), mapPos.Position);

        var ent = Spawn(component.ProjectilePrototype, spawnCoords);
        var direction = targetCoords.ToMapPos(EntityManager, _xform) - mapPos.Position;

        if (!TryComp<ProjectileComponent>(ent, out var comp))
            return;

        comp.Damage *= severity;

        _gunSystem.ShootProjectile(ent, direction, Vector2.Zero, uid, component.MaxProjectileSpeed * severity);
    }

    private void OnSupercritical(EntityUid uid, IceAnomalyComponent component, ref AnomalySupercriticalEvent args)
    {
        var xform = Transform(uid);
        var grid = xform.GridUid;
        var map = xform.MapUid;

        var indices = _xform.GetGridOrMapTilePosition(uid, xform);
        var mixture = _atmosphere.GetTileMixture(grid, map, indices, true);

        _boom.QueueExplosion(
            uid,
            component.ExplosionPrototype,
            component.TotalIntensity,
            component.Dropoff,
            component.MaxTileIntensity
        );

        ShootProjectilesAtEntites(uid, component, 1.0f, 1.0f);

        if (mixture == null)
            return;
        mixture.AdjustMoles(component.SupercriticalGas, component.SupercriticalMoleAmount);
        if (grid is { })
        {
            foreach (var ind in _atmosphere.GetAdjacentTiles(grid.Value, indices))
            {
                var mix = _atmosphere.GetTileMixture(grid, map, ind, true);
                if (mix is not { })
                    continue;

                mix.AdjustMoles(component.SupercriticalGas, component.SupercriticalMoleAmount);
                mix.Temperature += component.FreezeZoneExposeTemperature;
                _atmosphere.HotspotExpose(grid.Value, indices, component.FreezeZoneExposeTemperature, component.FreezeZoneExposeVolume, uid, true);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IceAnomalyComponent, AnomalyComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var ice, out var anom, out var xform))
        {
            var grid = xform.GridUid;
            var map = xform.MapUid;
            var indices = _xform.GetGridOrMapTilePosition(ent, xform);
            var mixture = _atmosphere.GetTileMixture(grid, map, indices, true);
            if (mixture is { })
            {
                mixture.Temperature += ice.ChillPerSecond * anom.Severity * frameTime;
            }

            if (grid != null && anom.Severity > ice.AnomalyFreezeZoneThreshold)
            {
                _atmosphere.HotspotExpose(grid.Value, indices, ice.FreezeZoneExposeTemperature, ice.AnomalyFreezeZoneThreshold, ent, true);
            }
        }
    }
}
