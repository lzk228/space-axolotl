using Content.Shared.Chemistry;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Artifact.XAT.Components;

namespace Content.Shared.Xenoarchaeology.Artifact;

public abstract partial class SharedXenoArtifactSystem
{
    private void InitializeXAT()
    {
        XATRelayLocalEvent<DamageChangedEvent>();
        XATRelayLocalEvent<InteractUsingEvent>();
        XATRelayLocalEvent<PullStartedMessage>();
        XATRelayLocalEvent<AttackedEvent>();
        XATRelayLocalEvent<XATToolUseDoAfterEvent>();
        XATRelayLocalEvent<InteractHandEvent>();
        XATRelayLocalEvent<ReactionEntityEvent>();

        // special case this one because we need to order the messages
        SubscribeLocalEvent<XenoArtifactComponent, ExaminedEvent>(OnExamined);
    }

    protected void XATRelayLocalEvent<T>() where T : notnull
    {
        SubscribeLocalEvent<XenoArtifactComponent, T>(RelayEventToNodes);
    }

    private void OnExamined(Entity<XenoArtifactComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(XenoArtifactComponent)))
        {
            RelayEventToNodes(ent, ref args);
        }
    }

    protected void RelayEventToNodes<T>(Entity<XenoArtifactComponent> ent, ref T args) where T : notnull
    {
        var ev = new XenoArchNodeRelayedEvent<T>(ent, args);

        var nodes = GetAllNodes(ent);
        foreach (var node in nodes)
        {
            RaiseLocalEvent(node, ref ev);
        }
    }

    public void TriggerXenoArtifact(Entity<XenoArtifactComponent> ent, Entity<XenoArtifactNodeComponent> node)
    {
        if (_timing.CurTime < ent.Comp.NextUnlockTime)
            return;

        if (!_unlockingQuery.TryGetComponent(ent, out var unlockingComp))
        {
            unlockingComp = EnsureComp<XenoArtifactUnlockingComponent>(ent);
            unlockingComp.EndTime = _timing.CurTime + ent.Comp.UnlockStateDuration;
            Log.Debug($"{ToPrettyString(ent)} entered unlocking state");

            if (_net.IsServer)
                _popup.PopupEntity(Loc.GetString("artifact-unlock-state-begin"), ent);
        }
        var index = GetIndex(ent, node);

        if (unlockingComp.TriggeredNodeIndexes.Add(index))
        {
            Dirty(ent, unlockingComp);
        }
    }
}

/// <summary>
/// Event wrapper for XenoArch Trigger events.
/// </summary>
[ByRefEvent]
public sealed class XenoArchNodeRelayedEvent<TEvent> : EntityEventArgs
{
    public Entity<XenoArtifactComponent> Artifact;

    public TEvent Args;

    public XenoArchNodeRelayedEvent(Entity<XenoArtifactComponent> artifact, TEvent args)
    {
        Artifact = artifact;
        Args = args;
    }
}
