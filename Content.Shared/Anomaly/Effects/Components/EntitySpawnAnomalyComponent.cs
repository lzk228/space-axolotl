using Content.Shared.Maps;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class EntitySpawnAnomalyComponent : Component
{
    /// <summary>
    /// A list of entities that are random picked to be spawned on each pulse
    /// </summary>
    [DataField("spawns", required: true, customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public List<string> Spawns = new();

    /// <summary>
    /// A list of entities that are random picked to be spawned when supercritical;
    /// </summary>
    [DataField("superCriticalSpawns", required: true, customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public List<string> SuperCriticalSpawns = new();

    /// <summary>
    /// The maximum number of entities that spawn per pulse
    /// scales with severity.
    /// </summary>
    [DataField("maxSpawnAmount"), ViewVariables(VVAccess.ReadWrite)]
    public int MaxSpawnAmount = 7;

    /// <summary>
    /// The maximum radius the entities will spawn in.
    /// Also governs the maximum reach of flesh tiles
    /// scales with stability
    /// </summary>
    [DataField("spawnRange"), ViewVariables(VVAccess.ReadWrite)]
    public float SpawnRange = 5f;

    /// <summary>
    /// The tile that is spawned by the anomaly's effect
    /// </summary>
    [DataField("floorTileId", customTypeSerializer: typeof(PrototypeIdSerializer<ContentTileDefinition>)), ViewVariables(VVAccess.ReadWrite)]
    public string FloorTileId = "FloorFlesh";

    /// <summary>
    /// The entity spawned when the anomaly goes supercritical
    /// </summary>
    [DataField("superCriticalSpawn", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string SupercriticalSpawn = "FleshKudzu";
}
