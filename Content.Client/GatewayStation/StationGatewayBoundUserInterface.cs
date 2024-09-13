using Content.Shared.GatewayStation;

namespace Content.Client.GatewayStation;

public sealed class StationGatewayBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private StationGatewayWindow? _menu;

    public StationGatewayBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        EntityUid? gridUid = null;
        var stationName = string.Empty;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;

            if (EntMan.TryGetComponent<MetaDataComponent>(gridUid, out var metaData))
            {
                stationName = metaData.EntityName;
            }
        }

        _menu = new StationGatewayWindow(this);
        _menu.OpenCentered();
        _menu.OnClose += Close;
        //this.CreateWindow<StationGatewayWindow>();
        _menu.Set(stationName, gridUid);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case StationGatewayState st:
                EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
                _menu?.ShowGateways(st.Gateways, Owner, xform?.Coordinates);
                break;
        }
    }

    public void SendGatewayLinkChangeMessage(NetEntity? gate)
    {
        SendMessage(new StationGatewayLinkChangeMessage(gate));
    }
}
