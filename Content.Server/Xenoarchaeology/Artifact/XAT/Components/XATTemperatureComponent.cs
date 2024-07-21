namespace Content.Server.Xenoarchaeology.Artifact.XAT.Components;

/// <summary>
/// This is used for an artifact that is activated by having a certain temperature near it.
/// </summary>
[RegisterComponent, Access(typeof(XATTemperatureSystem))]
public sealed partial class XATTemperatureComponent : Component
{
    /// <summary>
    /// The temperature that needs to be reached for the trigger
    /// </summary>
    [DataField]
    public float TargetTemperature;

    /// <summary>
    /// Whether or not the temp needs to be above or below the target.
    /// </summary>
    [DataField]
    public bool TriggerOnHigherTemp = true;
}
