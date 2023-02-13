using Robust.Shared.Audio;

namespace Content.Server.Reflect;

/// <summary>
///     Entities with this component have a chance to reflect projectiles and hitscan shots
/// </summary>
[RegisterComponent]
public sealed class ReflectComponent : Component
{
    /// <summary>
    ///     Can only reflect when enabled
    /// </summary>
    [DataField("enabled")]
    public bool Enabled;

    [DataField("chance")]
    public float Chance;

    [DataField("spread")]
    public Angle Spread = Angle.FromDegrees(5);

    [DataField("onReflect")]
    public SoundSpecifier? OnReflect { get; set; }
}
