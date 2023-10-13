using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Anomaly.Components;

/// <summary>
/// a device that allows you to translate anomaly activity into multitool signals.
/// </summary>
[RegisterComponent, Access(typeof(AnomalySynchronizerSystem))]
public sealed partial class AnomalySynchronizerComponent : Component
{
    /// <summary>
    /// The uid of the anomaly to which the synchronizer is connected.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? ConnectedAnomaly;


    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<SourcePortPrototype> DecayingPort = "Decaying";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<SourcePortPrototype> StabilizePort = "Stabilize";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<SourcePortPrototype> GrowingPort = "Growing";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<SourcePortPrototype> PulsePort = "Pulse";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<SourcePortPrototype> SupercritPort = "SuperCritical";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier ConnectedSound = new SoundPathSpecifier("/Audio/Machines/anomaly_sync_connect.ogg");

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier DisconnectedSound = new SoundPathSpecifier("/Audio/Machines/anomaly_sync_connect.ogg");
}
