using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Random;

/// <summary>
///     Data that specifies reagents that share the same weight and quantity for use with WeightedRandomSolution.
/// </summary>
[Serializable, NetSerializable]
[DataDefinition]
public sealed class RandomFill
{
    /// <summary>
    ///     Quantity of listed reagents.
    /// </summary>
    [DataField("quantity")]
    public FixedPoint2 Quantity = 0;

    /// <summary>
    ///     Random weight of listed reagents.
    /// </summary>
    [DataField("weight")]
    public float Weight = 0;

    /// <summary>
    ///     Listed reagents that the weight and quantity apply to.
    /// </summary>
    [DataField("reagents")]
    public List<string> Reagents = new();
}
