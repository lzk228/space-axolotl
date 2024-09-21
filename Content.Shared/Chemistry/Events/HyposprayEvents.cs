using Content.Shared.Inventory;

namespace Content.Shared.Chemistry.Hypospray.Events;

public abstract partial class BeforeHyposprayInjectsTarget : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid EntityUsingHypospray;
    public readonly EntityUid Hypospray;
    public EntityUid TargetGettingInjected;
    public string? InjectMessageOverride;

    public BeforeHyposprayInjectsTarget(EntityUid user, EntityUid hypospray, EntityUid target)
    {
        EntityUsingHypospray = user;
        Hypospray = hypospray;
        TargetGettingInjected = target;
        InjectMessageOverride = null;
    }
}

/// <summary>
///     This event is raised on the user using the hypospray before the hypospray is injected.
///     The event is triggered on the user and all their clothing.
/// </summary>
public sealed class SelfBeforeHyposprayInjects : BeforeHyposprayInjectsTarget
{
    public SelfBeforeHyposprayInjects(EntityUid user, EntityUid hypospray, EntityUid target) : base(user, hypospray, target) { }
}

/// <summary>
///     This event is raised on the target before the hypospray is injected.
///     The event is triggered on the target itself and all its clothing.
/// </summary>
public sealed class TargetBeforeHyposprayInjects : BeforeHyposprayInjectsTarget
{
    public TargetBeforeHyposprayInjects(EntityUid user, EntityUid hypospray, EntityUid target) : base(user, hypospray, target) { }
}
