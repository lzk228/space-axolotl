using System.Linq;
using Content.Shared.Access.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Clothing.EntitySystems;

public abstract class SharedChameleonClothingSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ClothingSystem _clothingSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedItemSystem _itemSystem = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] protected readonly IGameTiming _timing = default!;

    private static readonly SlotFlags[] IgnoredSlots =
    {
        SlotFlags.All,
        SlotFlags.PREVENTEQUIP,
        SlotFlags.NONE
    };
    private static readonly SlotFlags[] Slots = Enum.GetValues<SlotFlags>().Except(IgnoredSlots).ToArray();

    public readonly Dictionary<SlotFlags, List<string>> ValidVariants = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChameleonClothingComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<ChameleonClothingComponent, GotUnequippedEvent>(OnGotUnequipped);

        SubscribeLocalEvent<ChameleonClothingComponent, PrototypesReloadedEventArgs>(OnPrototypeReload);
        PrepareAllVariants();
    }

    private void OnPrototypeReload(EntityUid uid, ChameleonClothingComponent component, PrototypesReloadedEventArgs args)
    {
        PrepareAllVariants();
    }

    private void OnGotEquipped(EntityUid uid, ChameleonClothingComponent component, GotEquippedEvent args)
    {
        component.User = args.Equipee;
    }

    private void OnGotUnequipped(EntityUid uid, ChameleonClothingComponent component, GotUnequippedEvent args)
    {
        component.User = null;
    }

    // Updates chameleon visuals and meta information.
    // This function is called on a server after user selected new outfit.
    // And after that on a client after state was updated.
    // This 100% makes sure that server and client have exactly same data.
    protected void UpdateVisuals(EntityUid uid, ChameleonClothingComponent component)
    {
        if (string.IsNullOrEmpty(component.Default) ||
            !_proto.TryIndex(component.Default, out EntityPrototype? proto))
            return;

        // world sprite icon
        UpdateSprite(uid, proto);

        // copy name and description, unless its an ID card
        if (!HasComp<IdCardComponent>(uid))
        {
            var meta = MetaData(uid);
            _metaData.SetEntityName(uid, proto.Name, meta);
            _metaData.SetEntityDescription(uid, proto.Description, meta);
        }

        // item sprite logic
        if (TryComp(uid, out ItemComponent? item) &&
            proto.TryGetComponent(out ItemComponent? otherItem, _factory))
        {
            _itemSystem.CopyVisuals(uid, otherItem, item);
        }

        // clothing sprite logic
        if (TryComp(uid, out ClothingComponent? clothing) &&
            proto.TryGetComponent("Clothing", out ClothingComponent? otherClothing))
        {
            _clothingSystem.CopyVisuals(uid, otherClothing, clothing);
        }
    }

    protected virtual void UpdateSprite(EntityUid uid, EntityPrototype proto) { }

    /// <summary>
    ///     Check if this entity prototype is valid target for chameleon item.
    /// </summary>
    public bool IsValidTarget(EntityPrototype proto, SlotFlags chameleonSlot = SlotFlags.NONE)
    {
        // check if entity is valid
        if (proto.Abstract || proto.HideSpawnMenu)
            return false;

        // check if it is marked as valid chameleon target
        if (!proto.TryGetComponent(out TagComponent? tag, _factory) || !_tag.HasTag(tag, "WhitelistChameleon"))
            return false;

        // check if it's valid clothing
        if (!proto.TryGetComponent("Clothing", out ClothingComponent? clothing))
            return false;
        if (!clothing.Slots.HasFlag(chameleonSlot))
            return false;

        return true;
    }

    /// <summary>
    ///     Get a list of valid chameleon targets for these slots.
    /// </summary>
    public IEnumerable<string> GetValidTargets(SlotFlags slot)
    {
        var set = new HashSet<string>();
        foreach (var availableSlot in ValidVariants.Keys)
        {
            if (slot.HasFlag(availableSlot))
            {
                set.UnionWith(ValidVariants[availableSlot]);
            }
        }
        return set;
    }

    public void PrepareAllVariants()
    {
        ValidVariants.Clear();
        var prototypes = _proto.EnumeratePrototypes<EntityPrototype>();

        foreach (var proto in prototypes)
        {
            // check if this is valid clothing
            if (!IsValidTarget(proto))
                continue;
            if (!proto.TryGetComponent(out ClothingComponent? item, _factory))
                continue;

            // sort item by their slot flags
            // one item can be placed in several buckets
            foreach (var slot in Slots)
            {
                if (!item.Slots.HasFlag(slot))
                    continue;

                if (!ValidVariants.ContainsKey(slot))
                {
                    ValidVariants.Add(slot, new List<string>());
                }
                ValidVariants[slot].Add(proto.ID);
            }
        }
    }
}
