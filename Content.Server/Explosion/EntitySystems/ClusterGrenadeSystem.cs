using Content.Server.Explosion.Components;
using Content.Server.Flash.Components;
using Content.Shared.Explosion;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Content.Server.Weapons.Ranged.Systems;
using System.Numerics;

namespace Content.Server.Explosion.EntitySystems;

public sealed class ClusterGrenadeSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ThrowingSystem _throwingSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClusterGrenadeComponent, ComponentInit>(OnClugInit);
        SubscribeLocalEvent<ClusterGrenadeComponent, ComponentStartup>(OnClugStartup);
        SubscribeLocalEvent<ClusterGrenadeComponent, InteractUsingEvent>(OnClugUsing);
        SubscribeLocalEvent<ClusterGrenadeComponent, TriggerEvent>(OnClugTrigger);
    }

    private void OnClugInit(EntityUid uid, ClusterGrenadeComponent component, ComponentInit args)
    {
        component.GrenadesContainer = _container.EnsureContainer<Container>(uid, "cluster-flash");
    }

    private void OnClugStartup(EntityUid uid, ClusterGrenadeComponent component, ComponentStartup args)
    {
        if (component.FillPrototype != null)
        {
            component.UnspawnedCount = Math.Max(0, component.MaxGrenades - component.GrenadesContainer.ContainedEntities.Count);
            UpdateAppearance(uid, component);
        }
    }

    private void OnClugUsing(EntityUid uid, ClusterGrenadeComponent component, InteractUsingEvent args)
    {
        if (args.Handled) return;

        // TODO: Should use whitelist.
        if (component.GrenadesContainer.ContainedEntities.Count >= component.MaxGrenades ||
            !HasComp<FlashOnTriggerComponent>(args.Used))
            return;

        component.GrenadesContainer.Insert(args.Used);
        UpdateAppearance(uid, component);
        args.Handled = true;
    }

    private void OnClugTrigger(EntityUid uid, ClusterGrenadeComponent component, TriggerEvent args)
    {
        component.CountDown = true;
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ClusterGrenadeComponent>();

        while (query.MoveNext(out var uid, out var clug))
        {
            if (clug.CountDown && clug.UnspawnedCount > 0)
            {
                _audio.PlayPvs(clug.ReleaseSound, uid);
                var grenadesInserted = clug.GrenadesContainer.ContainedEntities.Count + clug.UnspawnedCount;
                var thrownCount = 0;
                var segmentAngle = 360 / grenadesInserted;
                var bombletDelay = 0;
                while (TryGetGrenade(clug, out var grenade))
                {
                    // var distance = random.NextFloat() * _throwDistance;
                    var angleMin = segmentAngle * thrownCount;
                    var angleMax = segmentAngle * (thrownCount + 1);
                    var angle = Angle.FromDegrees(_random.Next(angleMin, angleMax));
                    //var angle = _random.NextAngle();
                    bombletDelay += _random.Next(clug.BombletDelayMin, clug.BombletDelayMax);
                    thrownCount++;

                    if (clug.GrenadeType == "shoot")
                        if (clug.RandomSpread)
                            _gun.ShootProjectile(grenade, _random.NextVector2().Normalized(), Vector2.One.Normalized(), uid);
                        else _gun.ShootProjectile(grenade, angle.ToVec().Normalized(), Vector2.One.Normalized(), uid);
                    if (clug.GrenadeType == "throw")
                        if (clug.RandomSpread)
                            _throwingSystem.TryThrow(grenade, angle.ToVec().Normalized() * _random.NextFloat(0.1f, 3f), clug.BombletVelocity);
                        else _throwingSystem.TryThrow(grenade, angle.ToVec().Normalized() * clug.ThrowDistance, clug.BombletVelocity);

                    // give an active timer trigger to the contained grenades when they get launched
                    if (clug.TriggerBomblets)
                    {
                        var bomblet = grenade.EnsureComponent<ActiveTimerTriggerComponent>();
                        bomblet.TimeRemaining = (clug.MinimumDelay + bombletDelay) / 1000;
                        var ev = new ActiveTimerTriggerEvent(grenade, uid);
                        RaiseLocalEvent(uid, ref ev);
                    }
                }
                // delete the empty shell of the clusterbomb
                EntityManager.DeleteEntity(uid);
            }
        }
    }

    private bool TryGetGrenade(ClusterGrenadeComponent component, out EntityUid grenade)
    {
        grenade = default;

        if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            grenade = EntityManager.SpawnEntity(component.FillPrototype, Transform(component.Owner).MapPosition);
            return true;
        }

        if (component.GrenadesContainer.ContainedEntities.Count > 0)
        {
            grenade = component.GrenadesContainer.ContainedEntities[0];

            // This shouldn't happen but you never know.
            if (!component.GrenadesContainer.Remove(grenade))
                return false;

            return true;
        }

        return false;
    }

    private void UpdateAppearance(EntityUid uid, ClusterGrenadeComponent component)
    {
        if (!TryComp<AppearanceComponent>(component.Owner, out var appearance)) return;

        _appearance.SetData(uid, ClusterGrenadeVisuals.GrenadesCounter, component.GrenadesContainer.ContainedEntities.Count + component.UnspawnedCount, appearance);
    }
}
