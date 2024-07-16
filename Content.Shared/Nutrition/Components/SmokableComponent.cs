using Content.Shared.FixedPoint;
using Content.Shared.Smoking;
using Robust.Shared.GameStates;

namespace Content.Shared.Nutrition.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class SmokableComponent : Component
    {
        [DataField("solution")]
        public string Solution { get; private set; } = "smokable";

        [DataField("state")]
        public SmokableState State { get; set; } = SmokableState.Unlit;

        [DataField("exposeTemperature"), ViewVariables(VVAccess.ReadWrite)]
        public float ExposeTemperature { get; set; } = 0;

        [DataField("exposeVolume"), ViewVariables(VVAccess.ReadWrite)]
        public float ExposeVolume { get; set; } = 1f;

        // clothing prefixes
        [DataField("burntPrefix")]
        public string BurntPrefix = "unlit";
        [DataField("litPrefix")]
        public string LitPrefix = "lit";
        [DataField("unlitPrefix")]
        public string UnlitPrefix = "unlit";
    }
}
