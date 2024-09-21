using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Clothing.Components;

/// <summary>
///     Allow players to change clothing sprite to any other clothing prototype.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(SharedChameleonClothingSystem))]
public sealed partial class ChameleonClothingComponent : Component
{
    /// <summary>
    ///     Filter possible chameleon options by their slot flag.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField(required: true)]
    public SlotFlags Slot;

    /// <summary>
    ///     EntityPrototype id that chameleon item is trying to mimic.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId? Default;

    /// <summary>
    ///     Current user that wears chameleon clothing.
    /// </summary>
    [ViewVariables]
    public EntityUid? User;

    /// <summary>
    ///     Will component owner be affected by EMP pulses?
    /// </summary>
    [DataField]
    public bool EmpAffected = true;

    /// <summary>
    ///     Intensity of clothes change on EMP.
    ///     Can be interpreted as "How many times clothes will change every second?".
    ///     Useless without <see cref="EmpAffected"/> set to true.
    /// </summary>
    [ViewVariables]
    [DataField]
    public int EmpChangeIntensity = 7;

    /// <summary>
    ///     Should the EMP-change happen continiously, or only once?
    ///     (False = once, True = continiously)
    ///     Useless without <see cref="EmpAffected"/>
    /// </summary>
    [ViewVariables]
    [DataField]
    public bool EmpContinious = false;

    [AutoPausedField]
    [DataField]
    public TimeSpan NextEmpChange = TimeSpan.Zero; // When we need to change outfit next time
}

[Serializable, NetSerializable]
public sealed class ChameleonBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly SlotFlags Slot;
    public readonly string? SelectedId;

    public ChameleonBoundUserInterfaceState(SlotFlags slot, string? selectedId)
    {
        Slot = slot;
        SelectedId = selectedId;
    }
}

[Serializable, NetSerializable]
public sealed class ChameleonPrototypeSelectedMessage : BoundUserInterfaceMessage
{
    public readonly string SelectedId;

    public ChameleonPrototypeSelectedMessage(string selectedId)
    {
        SelectedId = selectedId;
    }
}

[Serializable, NetSerializable]
public enum ChameleonUiKey : byte
{
    Key
}
