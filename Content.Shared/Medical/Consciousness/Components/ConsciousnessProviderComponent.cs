﻿using Content.Shared.Medical.Consciousness.Systems;

namespace Content.Shared.Medical.Consciousness.Components;


[RegisterComponent, Access(typeof(ConsciousnessSystem))]
public sealed partial class ConsciousnessProviderComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? LinkedConsciousness;
}
