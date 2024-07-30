using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared.Atmos.Components;

[NetworkedComponent]
[AutoGenerateComponentState]
[RegisterComponent]
public sealed partial class GasMinerComponent : Component
{
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Enabled { get; set; } = true;

    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Idle { get; set; } = false;

    /// <summary>
    ///      If the number of moles in the external environment exceeds this number, no gas will be mined.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("maxExternalAmount")]
    public float MaxExternalAmount { get; set; } = float.PositiveInfinity;

    /// <summary>
    ///      If the pressure (in kPA) of the external environment exceeds this number, no gas will be mined.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("maxExternalPressure")]
    public float MaxExternalPressure { get; set; } = Atmospherics.GasMinerDefaultMaxExternalPressure;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("spawnGas")]
    public Gas? SpawnGas { get; set; } = null;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("spawnTemperature")]
    public float SpawnTemperature { get; set; } = Atmospherics.T20C;

    /// <summary>
    ///     Number of moles created per second when the miner is working.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("spawnAmount")]
    public float SpawnAmount { get; set; } = Atmospherics.MolesCellStandard * 20f;
}
