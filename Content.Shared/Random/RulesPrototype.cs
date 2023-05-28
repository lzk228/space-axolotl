using Content.Shared.Access;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared.Random;

/// <summary>
/// Rules-based item selection. Can be used for any sort of conditional selection
/// Every single condition needs to be true for this to be selected.
/// e.g. "choose maintenance audio if 90% of tiles nearby are maintenance tiles"
/// </summary>
[Prototype("rules")]
public sealed class RulesPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = string.Empty;

    [DataField("rules", required: true)]
    public List<RulesRule> Rules = new();
}

[ImplicitDataDefinitionForInheritors]
public abstract class RulesRule
{

}

/// <summary>
/// Returns true if the attached entity is in space.
/// </summary>
public sealed class InSpaceRule : RulesRule
{

}

/// <summary>
/// Checks for entities matching the whitelist in range.
/// This is more expensive than <see cref="NearbyComponentsRule"/> so prefer that!
/// </summary>
public sealed class NearbyEntitiesRule : RulesRule
{
    /// <summary>
    /// How many of the entity need to be nearby.
    /// </summary>
    [DataField("count")]
    public int Count = 1;

    [DataField("whitelist", required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField("range")]
    public float Range = 10f;
}

public sealed class NearbyTilesPercentRule : RulesRule
{
    [DataField("percent", required: true)]
    public float Percent;

    [DataField("tiles", required: true, customTypeSerializer:typeof(PrototypeIdListSerializer<ContentTileDefinition>))]
    public List<string> Tiles = new();

    [DataField("range")]
    public float Range = 10f;
}

/// <summary>
/// Always returns true. Used for fallbacks.
/// </summary>
public sealed class AlwaysTrueRule : RulesRule
{

}

/// <summary>
/// Returns true if griduid and mapuid match (AKA on 'planet').
/// </summary>
public sealed class OnMapGridRule : RulesRule
{

}

/// <summary>
/// Checks for an entity nearby with the specified access.
/// </summary>
public sealed class NearbyAccessRule : RulesRule
{
    /// <summary>
    /// Count of entities that need to be nearby.
    /// </summary>
    [DataField("count")]
    public int Count = 1;

    [DataField("access", required: true, customTypeSerializer: typeof(PrototypeIdListSerializer<AccessLevelPrototype>))]
    public List<string> Access = new();

    [DataField("range")]
    public float Range = 10f;
}

public sealed class NearbyComponentsRule : RulesRule
{
    [DataField("count")] public int Count;

    [DataField("components", required: true)]
    public ComponentRegistry Components = default!;

    [DataField("range")]
    public float Range = 10f;
}
