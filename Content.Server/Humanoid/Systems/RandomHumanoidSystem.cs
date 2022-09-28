using Content.Server.Humanoid.Components;
using Content.Server.RandomMetadata;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Humanoid.Systems;

/// <summary>
/// This handles...
/// </summary>
public sealed class RandomHumanoidSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    [Dependency] private readonly HumanoidSystem _humanoid = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<RandomHumanoidComponent, MapInitEvent>(OnMapInit,
            after: new []{ typeof(RandomMetadataSystem) });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var comp in EntityQuery<RandomHumanoidComponent>())
        {
            Del(comp.Owner);
        }
    }

    private void OnMapInit(EntityUid uid, RandomHumanoidComponent component, MapInitEvent args)
    {
        if (!_prototypeManager.TryIndex<RandomHumanoidPrototype>(component.RandomSettingsId, out var prototype))
        {
            return;
        }

        var profile = HumanoidCharacterProfile.Random(prototype.SpeciesBlacklist);
        var speciesProto = _prototypeManager.Index<SpeciesPrototype>(profile.Species);
        var humanoid = Spawn(speciesProto.Prototype, Transform(uid).Coordinates);

        if (prototype.RandomizeName)
        {
            MetaData(humanoid).EntityName = profile.Name;
        }
        else
        {
            // Hacky solution, as RandomMetadata only occurs after the entity is created.
            MetaData(humanoid).EntityName = MetaData(uid).EntityName;
        }

        _humanoid.LoadProfile(humanoid, profile);

        if (prototype.Components != null)
        {
            foreach (var entry in prototype.Components.Values)
            {
                var comp = (Component) _serialization.Copy(entry.Component);
                comp.Owner = humanoid;
                EntityManager.AddComponent(humanoid, comp, true);
            }
        }
    }
}
