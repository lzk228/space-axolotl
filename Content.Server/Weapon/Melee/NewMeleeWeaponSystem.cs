using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.Damage.Systems;
using Content.Server.Weapon.Melee.Components;
using Content.Server.Weapon.Melee.Events;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Weapon.Melee;

public sealed class NewMeleeWeaponSystem : SharedNewMeleeWeaponSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SolutionContainerSystem _solutions = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;

    public const float DamagePitchVariation = 0.05f;

    // TODO:
    // - Sprite lerping -> Check rotated eyes
    // - Eye kick?
    // - Better overlay
    // - Port
    // - CVars to toggle some stuff

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeChemicalInjectorComponent, MeleeHitEvent>(OnChemicalInjectorHit);
    }

    protected override void Popup(string message, EntityUid? uid, EntityUid? user)
    {
        if (uid == null)
            return;

        PopupSystem.PopupEntity(message, uid.Value, Filter.Pvs(uid.Value, entityManager: EntityManager).RemoveWhereAttachedEntity(e => e == user));
    }

    protected override void DoPreciseAttack(EntityUid user, ReleasePreciseAttackEvent ev, NewMeleeWeaponComponent component)
    {
        base.DoPreciseAttack(user, ev, component);

        // Can't attack yourself
        // Not in LOS.
        if (user == ev.Target ||
            Deleted(ev.Target) ||
            // For consistency with wide attacks stuff needs damageable.
            !HasComp<DamageableComponent>(ev.Target) ||
            !TryComp<TransformComponent>(ev.Target, out var targetXform) ||
            !_interaction.InRangeUnobstructed(user, ev.Target, component.Range))
        {
            return;
        }

        // Raise event before doing damage so we can cancel damage if the event is handled
        var hitEvent = new MeleeHitEvent(new List<EntityUid>() { ev.Target }, user, component.Damage);
        RaiseLocalEvent(component.Owner, hitEvent);

        if (hitEvent.Handled)
            return;

        var targets = new List<EntityUid>(1)
        {
            ev.Target
        };

        // For stuff that cares about it being attacked.
        RaiseLocalEvent(ev.Target, new AttackedEvent(component.Owner, user, targetXform.Coordinates));

        var modifiedDamage = DamageSpecifier.ApplyModifierSets(component.Damage + hitEvent.BonusDamage, hitEvent.ModifiersList);
        var damageResult = _damageable.TryChangeDamage(ev.Target, modifiedDamage);

        if (damageResult != null && damageResult.Total > FixedPoint2.Zero)
        {
            // If the target has stamina and is taking blunt damage, they should also take stamina damage based on their blunt to stamina factor
            if (damageResult.DamageDict.TryGetValue("Blunt", out var bluntDamage))
            {
                _stamina.TakeStaminaDamage(ev.Target, (bluntDamage * component.BluntStaminaDamageFactor).Float());
            }

            if (component.Owner == user)
                _adminLogger.Add(LogType.MeleeHit,
                    $"{ToPrettyString(user):user} melee attacked {ToPrettyString(ev.Target):target} using their hands and dealt {damageResult.Total:damage} damage");
            else
                _adminLogger.Add(LogType.MeleeHit,
                    $"{ToPrettyString(user):user} melee attacked {ToPrettyString(ev.Target):target} using {ToPrettyString(component.Owner):used} and dealt {damageResult.Total:damage} damage");

            PlayHitSound(ev.Target, GetHighestDamageSound(modifiedDamage, _protoManager), hitEvent.HitSoundOverride, component.HitSound);
        }
        else
        {
            if (hitEvent.HitSoundOverride != null)
            {
                Audio.PlayPvs(hitEvent.HitSoundOverride, component.Owner);
            }
            else
            {
                Audio.PlayPvs(component.NoDamageSound, component.Owner);
            }
        }

        if (damageResult != null)
        {
            RaiseNetworkEvent(new MeleeEffectEvent(targets), Filter.Pvs(targetXform.Coordinates, entityMan: EntityManager));
        }
    }

    protected override void DoWideAttack(EntityUid user, ReleaseWideAttackEvent ev, NewMeleeWeaponComponent component)
    {
        base.DoWideAttack(user, ev, component);

        // TODO: This is copy-paste as fuck with DoPreciseAttack
        if (!TryComp<TransformComponent>(user, out var userXform))
        {
            return;
        }

        var targetMap = ev.Coordinates.ToMap(EntityManager);

        if (targetMap.MapId != userXform.MapID)
        {
            return;
        }

        var userPos = userXform.WorldPosition;
        var direction = targetMap.Position - userPos;
        var distance = Math.Min(component.Range, direction.Length);

        // This should really be improved. GetEntitiesInArc uses pos instead of bounding boxes.
        var entities = ArcRayCast(userPos, direction.ToWorldAngle(), component.Angle, distance, userXform.MapID, user);

        if (entities.Count == 0)
            return;

        var targets = new List<EntityUid>();
        var damageQuery = GetEntityQuery<DamageableComponent>();

        foreach (var entity in entities)
        {
            if (entity == user ||
                !damageQuery.HasComponent(entity))
                continue;

            targets.Add(entity);
        }

        // Raise event before doing damage so we can cancel damage if the event is handled
        var hitEvent = new MeleeHitEvent(targets, user, component.Damage);
        RaiseLocalEvent(component.Owner, hitEvent);

        if (hitEvent.Handled)
            return;

        // For stuff that cares about it being attacked.
        foreach (var target in targets)
        {
            RaiseLocalEvent(target, new AttackedEvent(component.Owner, user, Transform(target).Coordinates));
        }

        var modifiedDamage = DamageSpecifier.ApplyModifierSets(component.Damage + hitEvent.BonusDamage, hitEvent.ModifiersList);
        var appliedDamage = new DamageSpecifier();

        foreach (var entity in targets)
        {
            RaiseLocalEvent(entity, new AttackedEvent(component.Owner, user, ev.Coordinates));

            var damageResult = _damageable.TryChangeDamage(entity, modifiedDamage);

            if (damageResult != null && damageResult.Total > FixedPoint2.Zero)
            {
                appliedDamage += damageResult;

                if (component.Owner == user)
                    _adminLogger.Add(LogType.MeleeHit,
                        $"{ToPrettyString(user):user} melee attacked {ToPrettyString(entity):target} using their hands and dealt {damageResult.Total:damage} damage");
                else
                    _adminLogger.Add(LogType.MeleeHit,
                        $"{ToPrettyString(user):user} melee attacked {ToPrettyString(entity):target} using {ToPrettyString(component.Owner):used} and dealt {damageResult.Total:damage} damage");
            }
        }

        if (entities.Count != 0)
        {
            if (appliedDamage.Total > FixedPoint2.Zero)
            {
                var target = entities.First();
                PlayHitSound(target, GetHighestDamageSound(modifiedDamage, _protoManager), hitEvent.HitSoundOverride, component.HitSound);
            }
            else
            {
                if (hitEvent.HitSoundOverride != null)
                {
                    Audio.PlayPvs(hitEvent.HitSoundOverride, component.Owner);
                }
                else
                {
                    Audio.PlayPvs(component.NoDamageSound, component.Owner);
                }
            }
        }

        if (appliedDamage.Total > FixedPoint2.Zero)
        {
            RaiseNetworkEvent(new MeleeEffectEvent(targets), Filter.Pvs(Transform(targets[0]).Coordinates, entityMan: EntityManager));
        }
    }

    private HashSet<EntityUid> ArcRayCast(Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId, EntityUid ignore)
    {
        // TODO: This is pretty sucky.
        var widthRad = arcWidth;
        var increments = 1 + 35 * (int) Math.Ceiling(widthRad / (2 * Math.PI));
        var increment = widthRad / increments;
        var baseAngle = angle - widthRad / 2;

        var resSet = new HashSet<EntityUid>();

        for (var i = 0; i < increments; i++)
        {
            var castAngle = new Angle(baseAngle + increment * i);
            var res = _physics.IntersectRay(mapId,
                new CollisionRay(position, castAngle.ToWorldVec(),
                    (int) (CollisionGroup.MobMask | CollisionGroup.Opaque)), range, ignore, false).ToList();

            if (res.Count != 0)
            {
                resSet.Add(res[0].HitEntity);
            }
        }

        return resSet;
    }

    protected override void DoLunge(EntityUid user, Vector2 localPos, string? animation)
    {
        RaiseNetworkEvent(new MeleeLungeEvent(user, localPos, animation), Filter.Pvs(user, entityManager: EntityManager).RemoveWhereAttachedEntity(e => e == user));
    }

    private void PlayHitSound(EntityUid target, string? type, SoundSpecifier? hitSoundOverride, SoundSpecifier? hitSound)
    {
        var playedSound = false;

        // Play sound based off of highest damage type.
        if (TryComp<MeleeSoundComponent>(target, out var damageSoundComp))
        {
            if (type == null && damageSoundComp.NoDamageSound != null)
            {
                Audio.PlayPvs(damageSoundComp.NoDamageSound, target, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (type != null && damageSoundComp.SoundTypes?.TryGetValue(type, out var damageSoundType) == true)
            {
                Audio.PlayPvs(damageSoundType, target, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (type != null && damageSoundComp.SoundGroups?.TryGetValue(type, out var damageSoundGroup) == true)
            {
                Audio.PlayPvs(damageSoundGroup, target, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
        }

        // Use weapon sounds if the thing being hit doesn't specify its own sounds.
        if (!playedSound)
        {
            if (hitSoundOverride != null)
            {
                Audio.PlayPvs(hitSoundOverride, target, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (hitSound != null)
            {
                Audio.PlayPvs(hitSound, target, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
        }

        // Fallback to generic sounds.
        if (!playedSound)
        {
            switch (type)
            {
                // Unfortunately heat returns caustic group so can't just use the damagegroup in that instance.
                case "Burn":
                case "Heat":
                case "Cold":
                    Audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/welder.ogg"), target, AudioParams.Default.WithVariation(DamagePitchVariation));
                    break;
                // No damage, fallback to tappies
                case null:
                    Audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/tap.ogg"), target, AudioParams.Default.WithVariation(DamagePitchVariation));
                    break;
                case "Brute":
                    Audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/smash.ogg"), target, AudioParams.Default.WithVariation(DamagePitchVariation));
                    break;
            }
        }
    }

    public static string? GetHighestDamageSound(DamageSpecifier modifiedDamage, IPrototypeManager protoManager)
    {
        var groups = modifiedDamage.GetDamagePerGroup(protoManager);

        // Use group if it's exclusive, otherwise fall back to type.
        if (groups.Count == 1)
        {
            return groups.Keys.First();
        }

        var highestDamage = FixedPoint2.Zero;
        string? highestDamageType = null;

        foreach (var (type, damage) in modifiedDamage.DamageDict)
        {
            if (damage <= highestDamage) continue;
            highestDamageType = type;
        }

        return highestDamageType;
    }

    private void OnChemicalInjectorHit(EntityUid owner, MeleeChemicalInjectorComponent comp, MeleeHitEvent args)
    {
        if (!_solutions.TryGetInjectableSolution(owner, out var solutionContainer))
            return;

        var hitBloodstreams = new List<BloodstreamComponent>();
        foreach (var entity in args.HitEntities)
        {
            if (Deleted(entity))
                continue;

            if (EntityManager.TryGetComponent<BloodstreamComponent?>(entity, out var bloodstream))
                hitBloodstreams.Add(bloodstream);
        }

        if (hitBloodstreams.Count < 1)
            return;

        var removedSolution = solutionContainer.SplitSolution(comp.TransferAmount * hitBloodstreams.Count);
        var removedVol = removedSolution.TotalVolume;
        var solutionToInject = removedSolution.SplitSolution(removedVol * comp.TransferEfficiency);
        var volPerBloodstream = solutionToInject.TotalVolume * (1 / hitBloodstreams.Count);

        foreach (var bloodstream in hitBloodstreams)
        {
            var individualInjection = solutionToInject.SplitSolution(volPerBloodstream);
            _bloodstream.TryAddToChemicals((bloodstream).Owner, individualInjection, bloodstream);
        }
    }
}
