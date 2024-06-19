using Content.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Lightning.Components;
/// <summary>
/// Handles how lightning acts and is spawned. Use the ShootLightning method to fire lightning from one user to a target.
/// </summary>
public abstract partial class SharedLightningComponent : Component
{
    /// <summary>
    /// Can this lightning arc?
    /// </summary>
    [DataField]
    public bool CanArc;

    /// <summary>
    /// How much should lightning arc in total?
    /// Controls the amount of bolts that will spawn.
    /// </summary>
    [DataField]
    public int MaxTotalArcs = 50;

    /// <summary>
    /// The prototype ID used for arcing bolts. Usually will be the same name as the main proto but it could be flexible.
    /// </summary>
    [DataField("lightningPrototype", customTypeSerializer:typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string LightningPrototype = "Lightning";

    /// <summary>
    /// The target that the lightning will Arc to.
    /// </summary>
    [DataField]
    public EntityUid? ArcTarget;

    /// <summary>
    /// How far should this lightning go?
    /// </summary>
    [DataField]
    public float MaxLength = 5f;

    /// <summary>
    /// List of targets that this collided with already
    /// </summary>
    [DataField]
    public HashSet<EntityUid> ArcTargets = new();

    /// <summary>
    /// What should this arc to?
    /// </summary>
    [DataField("collisionMask")]
    public int CollisionMask = (int) (CollisionGroup.MobMask | CollisionGroup.MachineMask);
}
