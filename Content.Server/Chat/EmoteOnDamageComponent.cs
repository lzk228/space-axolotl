namespace Content.Server.Chat;

using Content.Server.Chat.Systems;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

/// <summary>
/// Causes an entity to automatically emote when taking damage.
/// </summary>
[RegisterComponent, Access(typeof(EmoteOnDamageSystem))]
public sealed class EmoteOnDamageComponent : Component
{
    /// <summary>
    /// Chance of preforming an emote when taking damage and not on cooldown.
    /// </summary>
    [DataField("emoteChance"), ViewVariables(VVAccess.ReadWrite)]
    public float EmoteChance = 0.5f;

    /// <summary>
    /// A set of emotes that will be randomly picked from.
    /// <see cref="EmotePrototype"/>
    /// </summary>
    [DataField("emotes", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<EmotePrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public HashSet<string> Emotes = new();

    /// <summary>
    /// The simulation time of the last emote preformed due to taking damage.
    /// </summary>
    [DataField("lastEmoteTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastEmoteTime = TimeSpan.Zero;

    /// <summary>
    /// The cooldown between emotes.
    /// </summary>
    [DataField("emoteCooldown"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan EmoteCooldown = TimeSpan.FromSeconds(2);
}
