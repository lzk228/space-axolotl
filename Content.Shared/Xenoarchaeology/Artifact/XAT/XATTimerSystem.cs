using Content.Shared.Examine;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Artifact.XAT.Components;

namespace Content.Shared.Xenoarchaeology.Artifact.XAT;

public sealed class XATTimerSystem : BaseXATSystem<XATTimerComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XATTimerComponent, MapInitEvent>(OnMapInit);
        XATSubscribeDirectEvent<ExaminedEvent>(OnExamine);
    }

    private void OnMapInit(Entity<XATTimerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextActivation = Timing.CurTime + ent.Comp.Delay;
        Dirty(ent);
    }

    private void OnExamine(Entity<XenoArtifactComponent> artifact, Entity<XATTimerComponent, XenoArtifactNodeComponent> node, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("xenoarch-trigger-examine-timer",
            ("time", MathF.Ceiling((float) (node.Comp1.NextActivation - Timing.CurTime).TotalSeconds))));
    }

    protected override void UpdateXAT(Entity<XenoArtifactComponent> artifact, Entity<XATTimerComponent, XenoArtifactNodeComponent> node, float frameTime)
    {
        base.UpdateXAT(artifact, node, frameTime);

        if (Timing.CurTime > node.Comp1.NextActivation)
            Trigger(artifact, node);
    }

    // We handle the timer resetting here because we need to keep it updated even if the node isn't able to unlock.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var timerQuery = EntityQueryEnumerator<XATTimerComponent>();
        while (timerQuery.MoveNext(out var uid, out var timer))
        {
            if (Timing.CurTime < timer.NextActivation)
                continue;
            timer.NextActivation += timer.Delay;
            Dirty(uid, timer);
        }
    }
}
