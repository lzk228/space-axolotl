using Robust.Shared.GameStates;

namespace Content.Shared.Anomaly.Effects.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedGravityAnomalySystem))]
public sealed partial class GravityAnomalyComponent : Component
{
    /// <summary>
    /// The maximumum size the GravityWellComponent MaxRange can be.
    /// Is scaled linearly with stability.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxGravityWellRange = 10f;

    /// <summary>
    /// The maximum distance from which the anomaly
    /// can throw you via a pulse.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxThrowRange = 5f;

    /// <summary>
    /// The maximum strength the anomaly
    /// can throw you via a pulse
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxThrowStrength = 10;

    /// <summary>
    /// The maximum Intensity of the RadiationSourceComponent.
    /// Is scaled linearly with stability.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxRadiationIntensity = 3f;

    /// <summary>
    /// The minimum acceleration value for GravityWellComponent
    /// Is scaled linearly with stability.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinAccel = 2f;

    /// <summary>
    /// The maximum acceleration value for GravityWellComponent
    /// Is scaled linearly with stability.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxAccel = 7f;

    /// <summary>
    /// The minimum acceleration value for GravityWellComponent
    /// Is scaled linearly with stability.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinRadialAccel = 2f;

    /// <summary>
    /// The maximum acceleration value for GravityWellComponent
    /// Is scaled linearly with stability.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxRadialAccel = 5f;

    /// <summary>
    /// The range around the anomaly that will be spaced on supercritical.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SpaceRange = 3f;
}
