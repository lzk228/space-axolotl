using Content.Client.Atmos.Consoles;
using Content.Client.Message;
using Content.Client.Pinpointer.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Monitor;
using Content.Shared.Prototypes;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Client.Atmos.Console;

[GenerateTypedNameReferences]
public sealed partial class AtmosMonitoringConsoleWindow : FancyWindow
{
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _protoManager;
    private readonly SpriteSystem _spriteSystem;

    private EntityUid? _owner;
    private NetEntity? _focusEntity;
    private int? _focusNetId;

    private bool _autoScrollActive = false;
    private bool _autoScrollAwaitsUpdate = false;

    private ProtoId<NavMapBlipPrototype> _navMapConsolePid;
    private ProtoId<NavMapBlipPrototype> _gasPipeSensorPid;

    public AtmosMonitoringConsoleWindow(AtmosMonitoringConsoleBoundUserInterface userInterface, EntityUid? owner)
    {
        RobustXamlLoader.Load(this);
        _entManager = IoCManager.Resolve<IEntityManager>();
        _protoManager = IoCManager.Resolve<IPrototypeManager>();
        _spriteSystem = _entManager.System<SpriteSystem>();

        _navMapConsolePid = _protoManager.Index<NavMapBlipPrototype>("NavMapConsole");
        _gasPipeSensorPid = _protoManager.Index<NavMapBlipPrototype>("GasPipeSensor");

        // Pass the owner to nav map
        _owner = owner;
        NavMap.Owner = _owner;

        // Set nav map grid uid
        var stationName = Loc.GetString("atmos-monitoring-window-unknown-location");

        if (_entManager.TryGetComponent<TransformComponent>(owner, out var xform))
        {
            NavMap.MapUid = xform.GridUid;

            // Assign station name      
            if (_entManager.TryGetComponent<MetaDataComponent>(xform.GridUid, out var stationMetaData))
                stationName = stationMetaData.EntityName;

            var msg = new FormattedMessage();
            msg.TryAddMarkup(Loc.GetString("atmos-monitoring-window-station-name", ("stationName", stationName)), out _);

            StationName.SetMessage(msg);
        }

        else
        {
            StationName.SetMessage(stationName);
            NavMap.Visible = false;
        }

        // Set trackable entity selected action
        NavMap.TrackedEntitySelectedAction += SetTrackedEntityFromNavMap;

        // Update nav map
        NavMap.ForceNavMapUpdate();

        // Set tab container headers
        MasterTabContainer.SetTabTitle(0, Loc.GetString("atmos-monitoring-window-tab-networks"));

        // Set UI toggles
        ShowPipeNetwork.OnToggled += _ => OnShowPipeNetworkToggled();
        ShowGasPipeSensors.OnToggled += _ => OnShowGasPipeSensors();
    }

    #region Toggle handling

    private void OnShowPipeNetworkToggled()
    {
        if (_owner == null)
            return;

        if (!_entManager.TryGetComponent<AtmosMonitoringConsoleComponent>(_owner.Value, out var console))
            return;

        NavMap.ShowPipeNetwork = ShowPipeNetwork.Pressed;

        foreach (var (netEnt, device) in console.AtmosDevices)
        {
            if (device.NavMapBlip == _gasPipeSensorPid)
                continue;

            if (ShowPipeNetwork.Pressed)
                AddTrackedEntityToNavMap(device);

            else
                NavMap.TrackedEntities.Remove(netEnt);
        }
    }

    private void OnShowGasPipeSensors()
    {
        if (_owner == null)
            return;

        if (!_entManager.TryGetComponent<AtmosMonitoringConsoleComponent>(_owner.Value, out var console))
            return;

        foreach (var (netEnt, device) in console.AtmosDevices)
        {
            if (device.NavMapBlip != _gasPipeSensorPid)
                return;

            if (ShowGasPipeSensors.Pressed)
                AddTrackedEntityToNavMap(device);

            else
                NavMap.TrackedEntities.Remove(netEnt);
        }
    }

    #endregion

    public void UpdateUI
        (EntityCoordinates? consoleCoords,
        AtmosMonitoringConsoleEntry[] atmosNetworks)
    {
        if (_owner == null)
            return;

        if (!_entManager.TryGetComponent<AtmosMonitoringConsoleComponent>(_owner.Value, out var console))
            return;

        // Reset nav map values
        NavMap.TrackedCoordinates.Clear();
        NavMap.TrackedEntities.Clear();

        if (_focusEntity != null && !console.AtmosDevices.Any(x => x.Key == _focusEntity))
            ClearFocus();

        // Add tracked entities to the nav map
        if (NavMap.Visible)
        {
            foreach (var (netEnt, device) in console.AtmosDevices)
            {
                // Update the focus network ID, incase it has changed
                if (_focusEntity == netEnt)
                    SetFocus(netEnt, device.NetId);

                // Skip network devices if the toggled is off
                if (!ShowPipeNetwork.Pressed && device.NavMapBlip != _gasPipeSensorPid)
                    continue;

                // Skip gas pipe sensors if the toggle is off
                if (!ShowGasPipeSensors.Pressed && device.NavMapBlip == _gasPipeSensorPid)
                    continue;

                AddTrackedEntityToNavMap(device);
            }
        }

        // Show the monitor location
        var consoleNetEnt = _entManager.GetNetEntity(_owner);

        if (consoleCoords != null && consoleNetEnt != null)
        {
            var proto = _protoManager.Index(_navMapConsolePid);

            if (proto.TexturePaths != null && proto.TexturePaths.Any())
            {
                var texture = _spriteSystem.Frame0(new SpriteSpecifier.Texture(proto.TexturePaths[0]));
                var blip = new NavMapBlip(consoleCoords.Value, texture, proto.Color, proto.Blinks, proto.Selectable);
                NavMap.TrackedEntities[consoleNetEnt.Value] = blip;
            }
        }

        // Update the nav map
        NavMap.ForceNavMapUpdate();

        // Clear excess children from the tables
        while (AtmosNetworksTable.ChildCount > atmosNetworks.Length)
            AtmosNetworksTable.RemoveChild(AtmosNetworksTable.GetChild(AtmosNetworksTable.ChildCount - 1));

        // Update all entries in each table
        for (int index = 0; index < atmosNetworks.Length; index++)
        {
            var entry = atmosNetworks.ElementAt(index);
            UpdateUIEntry(entry, index, AtmosNetworksTable, console);
        }

        // Auto-scroll re-enable
        if (_autoScrollAwaitsUpdate)
        {
            _autoScrollActive = true;
            _autoScrollAwaitsUpdate = false;
        }
    }

    private void AddTrackedEntityToNavMap(AtmosDeviceNavMapData metaData)
    {
        var proto = _protoManager.Index(metaData.NavMapBlip);

        if (proto.TexturePaths == null || !proto.TexturePaths.Any())
            return;

        var idx = Math.Clamp((int)metaData.Direction / 2, 0, proto.TexturePaths.Length - 1);
        var texture = proto.TexturePaths.Length > 0 ? proto.TexturePaths[idx] : proto.TexturePaths[0];
        var color = proto.Color;

        if (_focusNetId != null && metaData.NetId != _focusNetId)
            color *= Color.DimGray;

        var blinks = proto.Blinks || _focusEntity == metaData.NetEntity;
        var coords = _entManager.GetCoordinates(metaData.NetCoordinates);
        var blip = new NavMapBlip(coords, _spriteSystem.Frame0(new SpriteSpecifier.Texture(texture)), color, blinks, proto.Selectable, proto.Scale);
        NavMap.TrackedEntities[metaData.NetEntity] = blip;
    }

    private void UpdateUIEntry(AtmosMonitoringConsoleEntry entry, int index, Control table, AtmosMonitoringConsoleComponent console)
    {
        // Make new UI entry if required
        if (index >= table.ChildCount)
        {
            var newEntryContainer = new AtmosMonitoringEntryContainer(entry.NetEntity, entry.NetId, _entManager.GetCoordinates(entry.Coordinates));

            // On click
            newEntryContainer.FocusButton.OnButtonUp += args =>
            {
                if (_focusEntity == newEntryContainer.NetEntity)
                {
                    ClearFocus();
                }

                else
                {
                    SetFocus(newEntryContainer.NetEntity, newEntryContainer.NetId);

                    if (newEntryContainer.Coordinates != null)
                        NavMap.CenterToCoordinates(newEntryContainer.Coordinates.Value);
                }

                // Update affected UI elements across all tables
                UpdateConsoleTable(console, AtmosNetworksTable, _focusEntity);
            };

            // Add the entry to the current table
            table.AddChild(newEntryContainer);
        }

        // Update values and UI elements
        var tableChild = table.GetChild(index);

        if (tableChild is not AtmosMonitoringEntryContainer)
        {
            table.RemoveChild(tableChild);
            UpdateUIEntry(entry, index, table, console);

            return;
        }

        var entryContainer = (AtmosMonitoringEntryContainer)tableChild;
        entryContainer.UpdateEntry(entry, entry.NetEntity == _focusEntity);
    }

    private void UpdateConsoleTable(AtmosMonitoringConsoleComponent console, Control table, NetEntity? currTrackedEntity)
    {
        foreach (var tableChild in table.Children)
        {
            if (tableChild is not AtmosAlarmEntryContainer)
                continue;

            var entryContainer = (AtmosAlarmEntryContainer)tableChild;

            if (entryContainer.NetEntity != currTrackedEntity)
                entryContainer.RemoveAsFocus();

            else if (entryContainer.NetEntity == currTrackedEntity)
                entryContainer.SetAsFocus();
        }
    }

    private void SetTrackedEntityFromNavMap(NetEntity? focusEntity)
    {
        if (focusEntity == null)
            return;

        if (!_entManager.TryGetComponent<AtmosMonitoringConsoleComponent>(_owner, out var console))
            return;

        foreach (var (netEnt, device) in console.AtmosDevices)
        {
            if (netEnt != focusEntity)
                continue;

            if (device.NavMapBlip != _gasPipeSensorPid)
                return;

            // Set new focus
            SetFocus(focusEntity.Value, device.NetId);

            // Get the scroll position of the selected entity on the selected button the UI
            ActivateAutoScrollToFocus();
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        AutoScrollToFocus();
    }

    private void ActivateAutoScrollToFocus()
    {
        _autoScrollActive = false;
        _autoScrollAwaitsUpdate = true;
    }

    private void AutoScrollToFocus()
    {
        if (!_autoScrollActive)
            return;

        var scroll = AtmosNetworksTable.Parent as ScrollContainer;
        if (scroll == null)
            return;

        if (!TryGetVerticalScrollbar(scroll, out var vScrollbar))
            return;

        if (!TryGetNextScrollPosition(out float? nextScrollPosition))
            return;

        vScrollbar.ValueTarget = nextScrollPosition.Value;

        if (MathHelper.CloseToPercent(vScrollbar.Value, vScrollbar.ValueTarget))
            _autoScrollActive = false;
    }

    private bool TryGetVerticalScrollbar(ScrollContainer scroll, [NotNullWhen(true)] out VScrollBar? vScrollBar)
    {
        vScrollBar = null;

        foreach (var child in scroll.Children)
        {
            if (child is not VScrollBar)
                continue;

            var castChild = child as VScrollBar;

            if (castChild != null)
            {
                vScrollBar = castChild;
                return true;
            }
        }

        return false;
    }

    private bool TryGetNextScrollPosition([NotNullWhen(true)] out float? nextScrollPosition)
    {
        nextScrollPosition = null;

        var scroll = AtmosNetworksTable.Parent as ScrollContainer;
        if (scroll == null)
            return false;

        var container = scroll.Children.ElementAt(0) as BoxContainer;
        if (container == null || container.Children.Count() == 0)
            return false;

        // Exit if the heights of the children haven't been initialized yet
        if (!container.Children.Any(x => x.Height > 0))
            return false;

        nextScrollPosition = 0;

        foreach (var control in container.Children)
        {
            if (control == null || control is not AtmosMonitoringEntryContainer)
                continue;

            if (((AtmosMonitoringEntryContainer)control).NetEntity == _focusEntity)
                return true;

            nextScrollPosition += control.Height;
        }

        // Failed to find control
        nextScrollPosition = null;

        return false;
    }

    private void SetFocus(NetEntity focusEntity, int focusNetId)
    {
        _focusEntity = focusEntity;
        _focusNetId = focusNetId;
        NavMap.FocusNetId = focusNetId;
    }

    private void ClearFocus()
    {
        _focusEntity = null;
        _focusNetId = null;
        NavMap.FocusNetId = null;
    }
}
