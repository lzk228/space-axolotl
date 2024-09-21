using Content.Server.Administration.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Clumsy;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Administration.Systems;

public sealed class SuperBonkSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly ClumsySystem _clumsySystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperBonkComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public void StartSuperBonk(EntityUid target, float delay = 0.1f, bool stopWhenDead = false)
    {

        //The other check in the code to stop when the target dies does not work if the target is already dead.
        if (stopWhenDead && TryComp<MobStateComponent>(target, out var mState))
        {
            if (mState.CurrentState == MobState.Dead)
                return;
        }

        // Permanently give the clumsy component.
        var hadClumsy = EnsureComp<ClumsyComponent>(target, out _);

        var tables = EntityQueryEnumerator<BonkableComponent>();
        var bonks = new Dictionary<EntityUid, BonkableComponent>();
        // This is done so we don't crash if something like a new table is spawned.
        while (tables.MoveNext(out var uid, out var comp))
        {
            bonks.Add(uid, comp);
        }

        var sComp = new SuperBonkComponent
        {
            Target = target,
            Tables = bonks.GetEnumerator(),
            RemoveClumsy = !hadClumsy,
            StopWhenDead = stopWhenDead,
        };

        AddComp(target, sComp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var comps = EntityQueryEnumerator<SuperBonkComponent>();

        while (comps.MoveNext(out var uid, out var comp))
        {
            comp.TimeRemaining -= frameTime;
            if (!(comp.TimeRemaining <= 0))
                continue;

            Bonk(comp);

            if (!(comp.Tables.MoveNext()))
            {
                RemComp<SuperBonkComponent>(comp.Target);
                continue;
            }

            comp.TimeRemaining = comp.InitialTime;
        }
    }

    private void Bonk(SuperBonkComponent comp)
    {
        var uid = comp.Tables.Current.Key;

        // It would be very weird for something without a transform component to have a bonk component
        // but just in case because I don't want to crash the server.
        if (!HasComp<TransformComponent>(uid) || !TryComp<ClumsyComponent>(comp.Target, out var clumsyComp))
            return;

        _transformSystem.SetCoordinates(comp.Target, Transform(uid).Coordinates);

        _clumsySystem.HitHeadOnTableClumsy((comp.Target, clumsyComp), uid);

        _audioSystem.PlayPvs(clumsyComp.ClumsySound, comp.Target);
    }

    private void OnMobStateChanged(EntityUid uid, SuperBonkComponent comp, MobStateChangedEvent args)
    {
        if (comp.StopWhenDead && args.NewMobState == MobState.Dead)
        {
            RemComp<SuperBonkComponent>(uid);
        }
    }
}
