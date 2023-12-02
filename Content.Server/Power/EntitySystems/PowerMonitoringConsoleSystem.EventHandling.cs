using Content.Server.GameTicking.Rules.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Power;
using Robust.Server.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using System.Linq;

namespace Content.Server.Power.EntitySystems;

internal sealed partial class PowerMonitoringConsoleSystem
{
    private HashSet<ICommonSession> _trackedSessions = new();

    private void OnComponentStartup(EntityUid uid, PowerMonitoringConsoleComponent component, ComponentStartup args)
    {
        var xform = Transform(uid);
        if (xform.GridUid == null)
            return;

        if (!_gridPowerCableChunks.ContainsKey(xform.GridUid.Value))
            RefreshPowerCableGrid(xform.GridUid.Value, Comp<MapGridComponent>(xform.GridUid.Value));

        if (!_gridPowerCableChunks.TryGetValue(xform.GridUid.Value, out var allChunks))
            return;

        component.AllChunks = allChunks;
        Dirty(uid, component);
    }

    private void OnDeviceAnchoringChanged(EntityUid uid, PowerMonitoringDeviceComponent component, AnchorStateChangedEvent args)
    {
        var xfrom = Transform(uid);
        var gridUid = xfrom.GridUid;

        if (gridUid == null)
            return;

        if (!_gridDevices.TryGetValue(gridUid.Value, out var _))
            _gridDevices[gridUid.Value] = new();

        if (args.Anchored)
        {
            _gridDevices[gridUid.Value].Add((uid, component));

            if (component.IsCollectionMasterOrChild)
            {
                AssignEntityToMasterGroup(uid, component, xfrom.Coordinates);
                AssignMastersToEntities(component.CollectionName);
            }
        }

        else
        {
            _gridDevices[gridUid.Value].Remove((uid, component));

            if (component.IsCollectionMasterOrChild)
            {
                RemoveEntityFromMasterGroup(uid, component);
                AssignMastersToEntities(component.CollectionName);
            }
        }
    }

    public void OnCableAnchorStateChanged(EntityUid uid, CableComponent component, CableAnchorStateChangedEvent args)
    {
        var xform = Transform(uid);

        if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        if (!_gridPowerCableChunks.TryGetValue(xform.GridUid.Value, out var allChunks))
            return;

        var tile = _sharedMapSystem.LocalToTile(xform.GridUid.Value, grid, xform.Coordinates);
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, SharedNavMapSystem.ChunkSize);

        if (!allChunks.TryGetValue(chunkOrigin, out var chunk))
        {
            chunk = new PowerCableChunk(chunkOrigin);
            allChunks[chunkOrigin] = chunk;
        }

        if (args.Anchored)
            AddPowerCableToTile(chunk, tile, component);

        else
            RemovePowerCableFromTile(chunk, tile, component);

        var query = AllEntityQuery<PowerMonitoringConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var console, out var entXform))
        {
            if (entXform.GridUid != xform.GridUid)
                continue;

            console.AllChunks = allChunks;
            Dirty(ent, console);
        }
    }

    public void OnNodeGroupRebuilt(EntityUid uid, PowerMonitoringDeviceComponent component, NodeGroupsRebuilt args)
    {
        if (component.IsCollectionMasterOrChild)
            AssignMastersToEntities(component.CollectionName);

        if (_rebuildingFocusNetwork)
            return;

        var query = AllEntityQuery<PowerMonitoringConsoleComponent>();
        while (query.MoveNext(out var ent, out var console))
        {
            if (console.Focus == uid)
                ResetPowerMonitoringConsoleFocus(ent, console);
        }
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        // Reassign tracked devices sitting on the old grid to the new grids
        if (_gridDevices.TryGetValue(args.Grid, out var devicesToReassign))
        {
            _gridDevices.Remove(args.Grid);
            _gridPowerCableChunks.Remove(args.Grid);

            foreach ((var ent, var entDevice) in devicesToReassign)
            {
                var entXform = Transform(ent);

                if (entXform.GridUid == null || !entXform.Anchored)
                    continue;

                if (!_gridDevices.ContainsKey(entXform.GridUid.Value))
                    _gridDevices[entXform.GridUid.Value] = new();

                _gridDevices[entXform.GridUid.Value].Add((ent, entDevice));

                // Note: no need to update master-child relations
                // This is handled when/if the node network is rebuilt 
            }

            var allGrids = args.NewGrids.ToList();

            if (!allGrids.Contains(args.Grid))
                allGrids.Add(args.Grid);

            // Refresh affected power cable grids
            foreach (var grid in allGrids)
                RefreshPowerCableGrid(grid, Comp<MapGridComponent>(grid));

            // Update power monitoring consoles that stand on an updated grid
            var query = AllEntityQuery<PowerMonitoringConsoleComponent, TransformComponent>();
            while (query.MoveNext(out var ent, out var console, out var entXform))
            {
                if (entXform.GridUid == null || !entXform.Anchored)
                    continue;

                if (!allGrids.Contains(entXform.GridUid.Value))
                    continue;

                if (!_gridPowerCableChunks.TryGetValue(entXform.GridUid.Value, out var allChunks))
                    continue;

                console.AllChunks = allChunks;
                Dirty(ent, console);
            }
        }
    }

    // Sends the list of tracked power monitoring devices to player sessions with one or more power monitoring consoles open
    // This expansion of PVS is needed so that meta and sprite data for these device are available to the the player
    // Out-of-range devices will be automatically removed from the player PVS when the UI closes
    private void OnExpandPvsEvent(ref ExpandPvsEvent ev)
    {
        if (!_trackedSessions.Contains(ev.Session))
            return;

        var uis = _userInterfaceSystem.GetAllUIsForSession(ev.Session);

        if (uis == null)
            return;

        var checkedGrids = new List<EntityUid>();

        foreach (var ui in uis)
        {
            if (ui.UiKey is PowerMonitoringConsoleUiKey)
            {
                var xform = Transform(ui.Owner);

                if (xform.GridUid == null || checkedGrids.Contains(xform.GridUid.Value))
                    continue;

                checkedGrids.Add(xform.GridUid.Value);

                if (!_gridDevices.TryGetValue(xform.GridUid.Value, out var gridDevices))
                    continue;

                if (ev.Entities == null)
                    ev.Entities = new List<EntityUid>();

                foreach ((var gridEnt, var device) in gridDevices)
                {
                    // Skip entities which are represented by a collection master
                    // This will cut down the number of entities that need to be added
                    if (device.IsCollectionMasterOrChild && !device.IsCollectionMaster)
                        continue;

                    ev.Entities.Add(gridEnt);
                }
            }
        }
    }

    private void OnBoundUIOpened(EntityUid uid, PowerMonitoringConsoleComponent component, BoundUIOpenedEvent args)
    {
        _trackedSessions.Add(args.Session);
    }

    private void OnBoundUIClosed(EntityUid uid, PowerMonitoringConsoleComponent component, BoundUIClosedEvent args)
    {
        var uis = _userInterfaceSystem.GetAllUIsForSession(args.Session);

        if (uis != null)
        {
            foreach (var ui in uis)
            {
                if (ui.UiKey is PowerMonitoringConsoleUiKey)
                    return;
            }
        }

        _trackedSessions.Remove(args.Session);
    }

    private void OnUpdateRequestReceived(EntityUid uid, PowerMonitoringConsoleComponent component, RequestPowerMonitoringUpdateMessage args)
    {
        var focus = EntityManager.GetEntity(args.FocusDevice);

        if (component.Focus != focus)
        {
            component.Focus = focus;
            _focusNetworkToBeRebuilt = true;
        }

        component.FocusGroup = args.FocusGroup;
    }

    private void OnPowerGridCheckStarted(ref GameRuleStartedEvent ev)
    {
        if (!TryComp<PowerGridCheckRuleComponent>(ev.RuleEntity, out var rule))
            return;

        var query = AllEntityQuery<PowerMonitoringConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var console, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station == rule.AffectedStation)
            {
                console.Flags |= PowerMonitoringFlags.PowerNetAbnormalities;
                Dirty(uid, console);
            }
        }
    }

    private void OnPowerGridCheckEnded(ref GameRuleEndedEvent ev)
    {
        if (!TryComp<PowerGridCheckRuleComponent>(ev.RuleEntity, out var rule))
            return;

        var query = AllEntityQuery<PowerMonitoringConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var console, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station == rule.AffectedStation)
            {
                console.Flags &= ~PowerMonitoringFlags.PowerNetAbnormalities;
                Dirty(uid, console);
            }
        }
    }
}
