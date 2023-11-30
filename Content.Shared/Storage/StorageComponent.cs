using Content.Shared.Item;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Storage
{
    /// <summary>
    /// Handles generic storage with window, such as backpacks.
    /// </summary>
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class StorageComponent : Component
    {
        public static string ContainerId = "storagebase";

        // TODO: This fucking sucks
        [ViewVariables(VVAccess.ReadWrite), DataField("isOpen"), AutoNetworkedField]
        public bool IsUiOpen;

        [ViewVariables]
        public Container Container = default!;

        /// <summary>
        /// A limit for the cumulative ItemSize weights that can be inserted in this storage.
        /// If MaxSlots is not null, then this is ignored.
        /// </summary>
        //todo kill
        [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
        public int MaxTotalWeight;

        //todo more doc
        //todo this could be a hashset instead of a dict
        [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
        public Dictionary<NetEntity, ItemStorageLocation> StoredItems = new();

        //todo doc
        [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
        public List<Box2i> StorageGrid = new();

        /// <summary>
        /// The maximum size item that can be inserted into this storage,
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
        [Access(typeof(SharedStorageSystem))]
        public ProtoId<ItemSizePrototype>? MaxItemSize;

        /// <summary>
        /// The max number of entities that can be inserted into this storage.
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
        public int? MaxSlots;

        // TODO: Make area insert its own component.
        [DataField("quickInsert")]
        public bool QuickInsert; // Can insert storables by "attacking" them with the storage entity

        [DataField("clickInsert")]
        public bool ClickInsert = true; // Can insert stuff by clicking the storage entity with it

        [DataField("areaInsert")]
        public bool AreaInsert;  // "Attacking" with the storage entity causes it to insert all nearby storables after a delay

        [DataField("areaInsertRadius")]
        public int AreaInsertRadius = 1;

        /// <summary>
        /// Whitelist for entities that can go into the storage.
        /// </summary>
        [DataField("whitelist")]
        public EntityWhitelist? Whitelist;

        /// <summary>
        /// Blacklist for entities that can go into storage.
        /// </summary>
        [DataField("blacklist")]
        public EntityWhitelist? Blacklist;

        /// <summary>
        /// Sound played whenever an entity is inserted into storage.
        /// </summary>
        [DataField("storageInsertSound")]
        public SoundSpecifier? StorageInsertSound = new SoundCollectionSpecifier("storageRustle");

        /// <summary>
        /// Sound played whenever an entity is removed from storage.
        /// </summary>
        [DataField("storageRemoveSound")]
        public SoundSpecifier? StorageRemoveSound;

        /// <summary>
        /// Sound played whenever the storage window is opened.
        /// </summary>
        [DataField("storageOpenSound")]
        public SoundSpecifier? StorageOpenSound = new SoundCollectionSpecifier("storageRustle");

        /// <summary>
        /// Sound played whenever the storage window is closed.
        /// </summary>
        [DataField("storageCloseSound")]
        public SoundSpecifier? StorageCloseSound;

        [Serializable, NetSerializable]
        public sealed class StorageInsertItemMessage : BoundUserInterfaceMessage
        {
        }

        [Serializable, NetSerializable]
        public enum StorageUiKey
        {
            Key,
        }
    }

    [Serializable, NetSerializable]
    public sealed class StorageInteractWithItemEvent : EntityEventArgs
    {
        public readonly NetEntity InteractedItemUid;

        public readonly NetEntity StorageUid;

        public StorageInteractWithItemEvent(NetEntity interactedItemUid, NetEntity storageUid)
        {
            InteractedItemUid = interactedItemUid;
            StorageUid = storageUid;
        }
    }

    [Serializable, NetSerializable]
    public sealed class StorageSetItemLocationEvent : EntityEventArgs
    {
        public readonly NetEntity ItemEnt;

        public readonly NetEntity StorageEnt;

        public readonly ItemStorageLocation Location;

        public StorageSetItemLocationEvent(NetEntity itemEnt, NetEntity storageEnt, ItemStorageLocation location)
        {
            ItemEnt = itemEnt;
            StorageEnt = storageEnt;
            Location = location;
        }
    }

    [Serializable, NetSerializable]
    public sealed class StorageInsertItemIntoLocationEvent : EntityEventArgs
    {
        public readonly NetEntity ItemEnt;

        public readonly NetEntity StorageEnt;

        public readonly ItemStorageLocation Location;

        public StorageInsertItemIntoLocationEvent(NetEntity itemEnt, NetEntity storageEnt, ItemStorageLocation location)
        {
            ItemEnt = itemEnt;
            StorageEnt = storageEnt;
            Location = location;
        }
    }


    /// <summary>
    /// Network event for displaying an animation of entities flying into a storage entity
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class AnimateInsertingEntitiesEvent : EntityEventArgs
    {
        public readonly NetEntity Storage;
        public readonly List<NetEntity> StoredEntities;
        public readonly List<NetCoordinates> EntityPositions;
        public readonly List<Angle> EntityAngles;

        public AnimateInsertingEntitiesEvent(NetEntity storage, List<NetEntity> storedEntities, List<NetCoordinates> entityPositions, List<Angle> entityAngles)
        {
            Storage = storage;
            StoredEntities = storedEntities;
            EntityPositions = entityPositions;
            EntityAngles = entityAngles;
        }
    }

    [NetSerializable]
    [Serializable]
    public enum StorageVisuals : byte
    {
        Open,
        HasContents,
        CanLock,
        Locked,
        StorageUsed,
        Capacity
    }
}
