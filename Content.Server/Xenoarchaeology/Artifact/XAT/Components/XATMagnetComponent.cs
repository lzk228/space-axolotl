using Robust.Shared.GameStates;

namespace Content.Server.Xenoarchaeology.Artifact.XAT.Components;

[RegisterComponent, Access(typeof(XATMagnetSystem))]
public sealed partial class XATMagnetComponent : Component
{
    /// <summary>
    /// how close to the magnet do you have to be?
    /// </summary>
    [DataField]
    public float MagnetRange = 40f;

    /// <summary>
    /// How close do active magboots have to be?
    /// This is smaller because they are weaker magnets
    /// </summary>
    [DataField]
    public float MagbootsRange = 2f;
}
