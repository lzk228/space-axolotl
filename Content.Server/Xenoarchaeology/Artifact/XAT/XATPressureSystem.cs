using Content.Server.Atmos.EntitySystems;
using Content.Server.Xenoarchaeology.Artifact.XAT.Components;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Artifact.XAT;

namespace Content.Server.Xenoarchaeology.Artifact.XAT;

public sealed class XATPressureSystem : BaseXATSystem<XATPressureComponent>
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    protected override void UpdateXAT(Entity<XenoArtifactComponent> artifact, Entity<XATPressureComponent, XenoArtifactNodeComponent> node, float frameTime)
    {
        base.UpdateXAT(artifact, node, frameTime);

        var xform = Transform(artifact);

        if (_atmosphere.GetTileMixture((artifact, xform)) is not { } mixture)
            return;

        var pressure = mixture.Pressure;
        if (pressure >= node.Comp1.MaxPressureThreshold || pressure <= node.Comp1.MinPressureThreshold)
        {
            Trigger(artifact, node);
        }
    }
}
