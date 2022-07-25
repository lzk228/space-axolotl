﻿namespace Content.Shared.Ensnaring.Components;
/// <summary>
/// Use this on something you want to use to ensnare an entity with
/// </summary>
public abstract class SharedEnsnaringComponent : Component
{
    /// <summary>
    /// How long it should take to free someone.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("freeTime")]
    public float FreeTime = 1.0f;

    /// <summary>
    /// How long it should take for an entity to free themselves.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("breakoutTime")]
    public float BreakoutTime = 3.0f;

    //TODO: Raise default value, make datafield required.
    /// <summary>
    /// How much should this slow down the entities walk?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("walkSpeed")]
    public float WalkSpeed = 0.3f;

    //TODO: Raise default value, make datafield required.
    /// <summary>
    /// How much should this slow down the entities sprint?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("sprintSpeed")]
    public float SprintSpeed = 0.3f;

    /// <summary>
    /// Should this ensnare someone when thrown?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("canThrowTrigger")]
    public bool CanThrowTrigger;

    /// <summary>
    /// What is ensnared?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("ensnared")]
    public EntityUid? Ensnared;
}

public sealed class EnsnareRemoveEvent : CancellableEntityEventArgs
{

}

public sealed class EnsnareChangeEvent : EntityEventArgs
{
    public readonly float WalkSpeed;
    public readonly float SprintSpeed;

    public EnsnareChangeEvent(float walkSpeed, float sprintSpeed)
    {
        WalkSpeed = walkSpeed;
        SprintSpeed = sprintSpeed;
    }
}
