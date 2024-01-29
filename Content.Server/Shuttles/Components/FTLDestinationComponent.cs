using Content.Shared.Whitelist;

namespace Content.Server.Shuttles.Components;

[RegisterComponent]
public sealed partial class FTLDestinationComponent : Component
{
    /// <summary>
    /// Should this destination be restricted in some form from console visibility.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Is this destination visible but available to be warped to?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool Enabled = true;

    /// <summary>
    /// Can we only FTL to beacons on this map.
    /// </summary>
    [DataField]
    public bool BeaconsOnly;
}
