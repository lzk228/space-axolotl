using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Humanoid;
using Content.Server.Polymorph.Systems;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

public sealed class PolyArtifactSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PolymorphSystem _poly = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PolyArtifactComponent, ArtifactActivatedEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, PolyArtifactComponent component, ArtifactActivatedEvent args)
    {
        foreach (var target in _lookup.GetEntitiesInRange(uid, component.Range))
        {
            if (HasComp<HumanoidAppearanceComponent>(target))
                _poly.PolymorphEntity(target, "ArtifactMonkey");
                _audio.PlayPvs(component.PolySound, uid);
        }
    }
}
