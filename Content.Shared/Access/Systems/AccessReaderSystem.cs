using Content.Shared.Access.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.GameTicking;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.IdentityManagement;
using Content.Shared.StationRecords;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared.Access.Systems
{
    public sealed class AccessReaderSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly SharedGameTicker _gameTicker = default!;
        [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
        [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessReaderComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<AccessReaderComponent, LinkAttemptEvent>(OnLinkAttempt);

        SubscribeLocalEvent<AccessReaderComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<AccessReaderComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnGetState(EntityUid uid, AccessReaderComponent component, ref ComponentGetState args)
    {
        args.State = new AccessReaderComponentState(component.Enabled, component.DenyTags, component.AccessLists,
            component.AccessKeys, component.AccessLog, component.AccessLogLimit);
    }

        private void OnHandleState(EntityUid uid, AccessReaderComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not AccessReaderComponentState state)
                return;
            component.Enabled = state.Enabled;
            component.AccessKeys = new (state.AccessKeys);
            component.AccessLists = new (state.AccessLists);
            component.DenyTags = new (state.DenyTags);
            component.AccessLog = new(state.AccessLog);
            component.AccessLogLimit = state.AccessLogLimit;
        }

    private void OnLinkAttempt(EntityUid uid, AccessReaderComponent component, LinkAttemptEvent args)
    {
        if (args.User == null) // AutoLink (and presumably future external linkers) have no user.
            return;
        if (!HasComp<EmaggedComponent>(uid) && !IsAllowed(args.User.Value, component))
            args.Cancel();
    }

    private void OnEmagged(EntityUid uid, AccessReaderComponent reader, ref GotEmaggedEvent args)
    {
        args.Handled = true;
        reader.Enabled = false;
        Dirty(reader);
    }

    /// <summary>
    /// Finds all AccessReaderComponents in the container of the
    /// required entity.
    /// </summary>
    /// <param name="target">The entity to search for a container</param>
    private bool FindAccessReadersInContainer(EntityUid target, AccessReaderComponent accessReader, out List<AccessReaderComponent> result)
    {
        result = new();
        if (accessReader.ContainerAccessProvider == null)
            return false;

        if (!_containerSystem.TryGetContainer(target, accessReader.ContainerAccessProvider, out var container))
            return false;

        foreach (var entity in container.ContainedEntities)
        {
            if (TryComp<AccessReaderComponent>(entity, out var entityAccessReader))
                result.Add(entityAccessReader);
        }

        return result.Any();
    }

    /// <summary>
    /// Searches the source for access tags
    /// then compares it with the all targets accesses to see if it is allowed.
    /// </summary>
    /// <param name="source">The entity that wants access.</param>
    /// <param name="target">The entity to search for an access reader</param>
    /// <param name="reader">Optional reader from the target entity</param>
    public bool IsAllowed(EntityUid source, EntityUid target, AccessReaderComponent? reader = null)
    {
        if (!Resolve(target, ref reader, false))
            return true;

        if (FindAccessReadersInContainer(target, reader, out var accessReaderList))
        {
            foreach (var access in accessReaderList)
            {
                if (IsAllowed(source, access))
                    return true;
            }

            return false;
        }

        return IsAllowed(source, reader);
    }
    /// <summary>
    /// Searches the given entity for access tags
    /// then compares it with the readers access list to see if it is allowed.
    /// </summary>
    /// <param name="entity">The entity that wants access.</param>
    /// <param name="reader">A reader from a different entity</param>
    public bool IsAllowed(EntityUid entity, AccessReaderComponent reader)
    {
        // Access reader is totally disabled, so access is always allowed.
        if (!reader.Enabled)
            return true;

        var allEnts = FindPotentialAccessItems(entity);

        if (AreAccessTagsAllowed(FindAccessTags(entity, allEnts), reader))
            return true;

        if (FindStationRecordKeys(entity, out var recordKeys, allEnts)
            && AreStationRecordKeysAllowed(recordKeys, reader))
            return true;

        return false;
    }

    /// <summary>
    /// Compares the given tags with the readers access list to see if it is allowed.
    /// </summary>
    /// <param name="accessTags">A list of access tags</param>
    /// <param name="reader">An access reader to check against</param>
    /// <param name="logAccess">Should successful access be logged</param>
        public bool AreAccessTagsAllowed(IEnumerable<string> accessTags, AccessReaderComponent reader, bool logAccess = true)
        {
            var providedTags = new ProvidedAccessList();
            providedTags.AddAccessTags("", accessTags, !logAccess);

            return AreAccessTagsAllowed(providedTags, reader, logAccess);
        }

        /// <summary>
        /// Compares the given tags with the readers access list to see if it is allowed.
        /// </summary>
        /// <param name="accessTagsList">A list of access tags</param>
        /// <param name="reader">An access reader to check against</param>
        /// <param name="logAccess">Should successful access be logged</param>
        public bool AreAccessTagsAllowed(ProvidedAccessList accessTagsList, AccessReaderComponent reader, bool logAccess = true)
    {
        if (accessTagsList.HasAnyAccessTags(reader.DenyTags, this, logAccess ? reader : null) != null)
        {
            // Sec owned by cargo.

            // Note that in resolving the issue with only one specific item "counting" for access, this became a bit more strict.
            // As having an ID card in any slot that "counts" with a denied access group will cause denial of access.
            // DenyTags doesn't seem to be used right now anyway, though, so it'll be dependent on whoever uses it to figure out if this matters.
            return false;
        }

        return reader.AccessLists.Count == 0 || reader.AccessLists.Any(a => accessTagsList.HasAllAccessTags(a, this, logAccess ? reader : null) != null);
    }

    /// <summary>
    /// Compares the given stationrecordkeys with the accessreader to see if it is allowed.
    /// </summary>
    public bool AreStationRecordKeysAllowed(ICollection<StationRecordKey> keys, AccessReaderComponent reader)
    {
        return keys.Any() && reader.AccessKeys.Any(keys.Contains);
    }

    /// <summary>
    /// Finds all the items that could potentially give access to a given entity
    /// </summary>
    public HashSet<EntityUid> FindPotentialAccessItems(EntityUid uid)
    {
        FindAccessItemsInventory(uid, out var items);

        var ev = new GetAdditionalAccessEvent
        {
            Entities = items
        };
        RaiseLocalEvent(uid, ref ev);
        items.Add(uid);
        return items;
    }

    /// <summary>
    /// Finds the access tags on the given entity
    /// </summary>
    /// <param name="uid">The entity that is being searched.</param>
    /// <param name="items">All of the items to search for access. If none are passed in, <see cref="FindPotentialAccessItems"/> will be used.</param>
    public ProvidedAccessList FindAccessTags(EntityUid uid, HashSet<EntityUid>? items = null)
    {
        ProvidedAccessList? tags = null;
        var owned = false;

        items ??= FindPotentialAccessItems(uid);

        foreach (var ent in items)
        {
            FindAccessTagsItem(ent, ref tags, ref owned);
        }

        return tags ?? new ProvidedAccessList();
    }

    /// <summary>
    /// Finds the access tags on the given entity
    /// </summary>
    /// <param name="uid">The entity that is being searched.</param>
    /// <param name="items">All of the items to search for access. If none are passed in, <see cref="FindPotentialAccessItems"/> will be used.</param>
    public bool FindStationRecordKeys(EntityUid uid, out ICollection<StationRecordKey> recordKeys, HashSet<EntityUid>? items = null)
    {
        recordKeys = new HashSet<StationRecordKey>();

        items ??= FindPotentialAccessItems(uid);

        foreach (var ent in items)
        {
            if (FindStationRecordKeyItem(ent, out var key))
                recordKeys.Add(key.Value);
        }

        return recordKeys.Any();
    }

    /// <summary>
    ///     Try to find <see cref="AccessComponent"/> on this item
    ///     or inside this item (if it's pda)
    ///     This version merges into a set or replaces the set.
    ///     If owned is false, the existing tag-set "isn't ours" and can't be merged with (is read-only).
    /// </summary>
    private void FindAccessTagsItem(EntityUid uid, ref ProvidedAccessList? tags, ref bool owned)
    {
        if (!FindAccessTagsItem(uid, out var targetTags))
        {
            // no tags, no problem
            return;
        }
        if (tags != null)
        {
            // existing tags, so copy to make sure we own them
            if (!owned)
            {
                tags = new ProvidedAccessList();
                owned = true;
            }
            // then merge
            tags.AddAccessTags(targetTags);
        }
        else
        {
            // no existing tags, so now they're ours
            tags = new ProvidedAccessList();
                tags.AddAccessTags(targetTags);
            owned = false;
        }
    }

    public bool FindAccessItemsInventory(EntityUid uid, out HashSet<EntityUid> items)
    {
        items = new();

        foreach (var item in _handsSystem.EnumerateHeld(uid))
        {
            items.Add(item);
        }

        // maybe its inside an inventory slot?
        if (_inventorySystem.TryGetSlotEntity(uid, "id", out var idUid))
        {
            items.Add(idUid.Value);
        }

        return items.Any();
    }

    /// <summary>
    ///     Try to find <see cref="AccessComponent"/> on this item
    ///     or inside this item (if it's pda)
    /// </summary>
    private bool FindAccessTagsItem(EntityUid uid, [NotNullWhen(true)] out ProvidedAccessList? tags)
    {
            tags = new ProvidedAccessList();
        if (TryComp(uid, out AccessComponent? access))
        {
            tags.AddAccessTags(Identity.Name(uid, EntityManager), access.Tags, access.BypassLogging);
            return true;
        }

        if (TryComp(uid, out PdaComponent? pda) &&
            pda.ContainedId is { Valid: true } id)
        {
            var accessComponent = EntityManager.GetComponent<AccessComponent>(id);
                tags.AddAccessTags(Identity.Name(id, EntityManager), accessComponent.Tags, accessComponent.BypassLogging);
            return true;
        }

        tags = null;
        return false;
    }

    /// <summary>
    ///     Try to find <see cref="StationRecordKeyStorageComponent"/> on this item
    ///     or inside this item (if it's pda)
    /// </summary>
    private bool FindStationRecordKeyItem(EntityUid uid, [NotNullWhen(true)] out StationRecordKey? key)
    {
        if (TryComp(uid, out StationRecordKeyStorageComponent? storage) && storage.Key != null)
        {
            key = storage.Key;
            return true;
        }

        if (TryComp<PdaComponent>(uid, out var pda) &&
            pda.ContainedId is { Valid: true } id)
        {
            if (TryComp<StationRecordKeyStorageComponent>(id, out var pdastorage) && pdastorage.Key != null)
            {
                key = pdastorage.Key;
                return true;
            }
        }

            key = null;
            return false;
        }

        /// <summary>
        /// Logs an access
        /// </summary>
        /// <param name="reader">The reader to log the access on</param>
        /// <param name="provider">The accessor to log</param>
        private void LogAccess(AccessReaderComponent? reader, string provider)
        {
            if (reader == null)
                return;

            if (reader.AccessLog.Count >= reader.AccessLogLimit)
                reader.AccessLog.Dequeue();
            reader.AccessLog.Enqueue(new AccessRecord(_gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan), provider));
        }

        public record ProvidedAccessList()
        {
            private readonly Dictionary<string, ProvidedAccess> _accesses = new();

            private record struct ProvidedAccess(HashSet<string> AccessTags, bool BypassLogging);

            /// <summary>
            /// Attempts to check for any access tag
            /// </summary>
            /// <param name="accessTag">Tag to check for</param>
            /// <param name="accessReaderSystem"></param>
            /// <param name="reader"><see cref="AccessReaderComponent"/> to log the access to</param>
            /// <returns>The name of the matching access provider if the access check passes</returns>
            public string? HasAnyAccessTags(string accessTag, AccessReaderSystem accessReaderSystem, AccessReaderComponent? reader = null)
            {
                foreach (var (provider, providedAccess) in _accesses)
                {
                    if (!providedAccess.AccessTags.Contains(accessTag))
                        continue;

                    if (!providedAccess.BypassLogging)
                        accessReaderSystem.LogAccess(reader, provider);
                    return provider;
                }

                return null;
            }

            /// <summary>
            /// Attempts to check for any access tags
            /// </summary>
            /// <param name="accessTags">Tags to check for</param>
            /// <param name="accessReaderSystem"></param>
            /// <param name="reader"><see cref="AccessReaderComponent"/> to log the access to</param>
            /// <returns>The name of the matching access provider if the access check passes</returns>
            public string? HasAnyAccessTags(IReadOnlyCollection<string> accessTags, AccessReaderSystem accessReaderSystem, AccessReaderComponent? reader = null)
            {
                foreach (var (provider, providedAccess) in _accesses)
                {
                    if (!providedAccess.AccessTags.Overlaps(accessTags))
                        continue;

                    if (!providedAccess.BypassLogging)
                        accessReaderSystem.LogAccess(reader, provider);
                    return provider;
                }

                return null;
            }

            /// <summary>
            /// Attempts to check for all access tags
            /// </summary>
            /// <param name="accessTags">Tags to check for</param>
            /// <param name="accessReaderSystem"></param>
            /// <param name="reader"><see cref="AccessReaderComponent"/> to log the access to</param>
            /// <returns>The name of the matching access provider if the access check passes</returns>
            public string? HasAllAccessTags(HashSet<string> accessTags, AccessReaderSystem accessReaderSystem, AccessReaderComponent? reader = null)
            {
                foreach (var (provider, providedAccess) in _accesses)
                {
                    if (!accessTags.IsSubsetOf(providedAccess.AccessTags))
                        continue;

                    if (!providedAccess.BypassLogging)
                        accessReaderSystem.LogAccess(reader, provider);
                    return provider;
                }

                return null;
            }

            /// <summary>
            /// Adds access tags to the list of providable tags
            /// </summary>
            /// <param name="provider">Provider of the tags</param>
            /// <param name="accessTags">Tags being provided</param>
            /// <param name="bypassLogging">Determines if the access provider provide access without being logged</param>
            public void AddAccessTags(string provider, IEnumerable<string> accessTags, bool bypassLogging)
            {
                if (_accesses.TryGetValue(provider, out var access))
                    access.AccessTags.UnionWith(accessTags);
                else
                    _accesses.Add(provider, new ProvidedAccess(new HashSet<string>(accessTags), bypassLogging));
            }

            /// <summary>
            /// Merges <paramref name="providedAccessList"/>
            /// </summary>
            public void AddAccessTags(ProvidedAccessList providedAccessList)
            {
                foreach (var (provider, accessTags) in providedAccessList._accesses)
                {
                    AddAccessTags(provider, accessTags.AccessTags, accessTags.BypassLogging);
                }
            }

            /// <summary>
            /// Gets all providable tags
            /// </summary>
            /// <returns>Every access tag that can be provided</returns>
            public HashSet<string> AllTags()
            {
                var allTags = new HashSet<string>();

                foreach (var (_, accessTags) in _accesses)
                {
                    allTags.UnionWith(accessTags.AccessTags);
                }

                return allTags;
            }
        }
    }
}
