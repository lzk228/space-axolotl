using Content.Shared.Chemistry.Hypospray.Events;
using Content.Shared.Medical;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Climbing.Events;
using Robust.Shared.Random;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Damage;
using Robust.Shared.Timing;
using Content.Shared.IdentityManagement;
using Content.Shared.CCVar;
using Content.Shared.Climbing.Components;
using Robust.Shared.Configuration;

namespace Content.Shared.Clumsy;

public sealed class ClumsySystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ClumsyComponent, SelfBeforeHyposprayInjects>(BeforeHyposprayEvent);
        SubscribeLocalEvent<ClumsyComponent, SelfBeforeDefibrillatorZaps>(BeforeDefibrillatorZapsEvent);
        SubscribeLocalEvent<ClumsyComponent, SelfBeforeGunShotEvent>(BeforeGunShotEvent);
        SubscribeLocalEvent<ClumsyComponent, SelfBeforeClimbEvent>(OnBeforeClimbEvent);
    }

    // If you add more clumsy interactions add them in this section!
    #region Clumsy interaction events
    private void BeforeHyposprayEvent(Entity<ClumsyComponent> ent, ref SelfBeforeHyposprayInjects args)
    {
        // Clumsy people sometimes inject themselves! Apparently syringes are clumsy proof...
        if (!_random.Prob(ent.Comp.ClumsyDefaultCheck))
            return;

        args.TargetGettingInjected = args.EntityUsingHypospray;
        args.InjectMessageOverride = "hypospray-component-inject-self-clumsy-message";
        _audio.PlayPvs(ent.Comp.ClumsySound, ent);
    }

    private void BeforeDefibrillatorZapsEvent(Entity<ClumsyComponent> ent, ref SelfBeforeDefibrillatorZaps args)
    {
        // Clumsy people sometimes defib themselves!
        if (!_random.Prob(ent.Comp.ClumsyDefaultCheck))
            return;

        args.DefibTarget = args.EntityUsingDefib;
        _audio.PlayPvs(ent.Comp.ClumsySound, ent);

    }

    private void BeforeGunShotEvent(Entity<ClumsyComponent> ent, ref SelfBeforeGunShotEvent args)
    {
        // Clumsy people sometimes can't shoot :(

        if (args.Gun.Comp.ClumsyProof == true)
            return;

        if (!_random.Prob(ent.Comp.ClumsyDefaultCheck))
            return;

        _stun.TryParalyze(ent, TimeSpan.FromSeconds(3f), true);

        // Apply salt to the wound ("Honk!") (No idea what this comment means)
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/bang.ogg"), ent);
        _audio.PlayPvs(ent.Comp.ClumsySound, ent);

        _popup.PopupEntity(Loc.GetString("gun-clumsy"), ent, ent);
        args.Cancel();
    }

    private void OnBeforeClimbEvent(Entity<ClumsyComponent> ent, ref SelfBeforeClimbEvent args)
    {
        // This event is called in shared, thats why it has all the extra prediction stuff.
        var rand = new System.Random((int)_timing.CurTick.Value);

        // If someone is putting you on the table, always get past the guard.
        if (!_cfg.GetCVar(CCVars.GameTableBonk) && args.PuttingOnTable == ent.Owner && !rand.Prob(ent.Comp.ClumsyDefaultCheck))
            return;

        HitHeadOnTableClumsy(ent, args.BeingClimbedOn);

        _audio.PlayPredicted(ent.Comp.ClumsySound, ent, ent);

        var secondSound = new SoundCollectionSpecifier("TrayHit");
        _audio.PlayPredicted(secondSound, ent, ent);


        var beingClimbedName = Identity.Entity(args.BeingClimbedOn, EntityManager);
        var gettingPutOnTableName = Identity.Entity(args.GettingPutOnTable, EntityManager);
        var puttingOnTableName = Identity.Entity(args.PuttingOnTable, EntityManager);

        if (args.PuttingOnTable == ent.Owner)
            // You are slamming yourself onto the table.
            _popup.PopupClient(Loc.GetString("bonkable-success-message-user", ("bonkable", beingClimbedName)), ent, ent);
        else
            // Someone else slamed you onto the table.
            _popup.PopupPredicted(Loc.GetString("bonkable-success-message-others", ("bonker", puttingOnTableName), ("victim", gettingPutOnTableName), ("bonkable", beingClimbedName)), ent, ent);

        args.Cancel();
    }
    #endregion

    #region Helper functions
    /// <summary>
    ///     "Hits" an entites head against the given table.
    /// </summary>
    // Oh this fucntion is public le- NO!! This is only public for the one admin command if you use this anywhere else I will cry.
    public void HitHeadOnTableClumsy(Entity<ClumsyComponent> target, EntityUid table)
    {
        var stunTime = target.Comp.ClumsyDefaultStunTime;

        if (TryComp<BonkableComponent>(table, out var bonkComp) && bonkComp != null)
        {
            stunTime = bonkComp.BonkTime;
            _damageable.TryChangeDamage(target, bonkComp.BonkDamage, true);
        }

        _stun.TryParalyze(target, stunTime, true);
    }
    #endregion
}
