using Content.Server.DeviceLinking.Systems;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.GatewayStation.Components;
using Content.Shared.GatewayStation;
using Content.Shared.Pinpointer;
using Content.Shared.Teleportation.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.GatewayStation.Systems;

public sealed class StationGatewaySystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationGatewayConsoleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<StationGatewayConsoleComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<StationGatewayConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);

        SubscribeLocalEvent<StationGatewayConsoleComponent, StationGatewayLinkChangeMessage>(OnUILinkChanged);

        SubscribeLocalEvent<StationGatewayComponent, MapInitEvent>(OnGatewayMapInit);
    }

    private void OnUILinkChanged(Entity<StationGatewayConsoleComponent> ent, ref StationGatewayLinkChangeMessage args)
    {
        Log.Error("CLICK");
    }

    private void OnGatewayMapInit(Entity<StationGatewayComponent> ent, ref MapInitEvent args)
    {

    }

    private void OnRemove(Entity<StationGatewayConsoleComponent> ent, ref ComponentRemove args)
    {
        //Clear data
    }

    private void OnPacketReceived(Entity<StationGatewayConsoleComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        var payload = args.Data;

        //Update data

        UpdateUserInterface(ent);
    }

    private void OnUIOpened(Entity<StationGatewayConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUserInterface(ent);
    }

    private void UpdateUserInterface(Entity<StationGatewayConsoleComponent> ent)
    {
        if (!_uiSystem.IsUiOpen(ent.Owner, StationGatewayUIKey.Key))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        var xform = Transform(ent);

        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        //Send data
        List<StationGatewayStatus> gatewaysData = new();

        var query = EntityQueryEnumerator<StationGatewayComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gate, out var xformComp))
        {
            if (xform.GridUid != Transform(ent).GridUid)
                return;

            _link.GetLink(uid, out var link);
            EntityCoordinates? linkCoord = null;

            if (link is not null)
                linkCoord = Transform(link.Value).Coordinates;

            gatewaysData.Add(
                new(GetNetEntity(uid),
                    GetNetCoordinates(xformComp.Coordinates),
                    GetNetCoordinates(linkCoord),
                    gate.GateName));
        }
        _uiSystem.SetUiState(ent.Owner, StationGatewayUIKey.Key, new StationGatewayState(gatewaysData));
    }
}
