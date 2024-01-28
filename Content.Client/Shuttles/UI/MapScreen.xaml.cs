using System.Numerics;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Systems;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Shuttles.UI;

[GenerateTypedNameReferences]
public sealed partial class MapScreen : BoxContainer
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    private SharedTransformSystem _xformSystem;

    private EntityUid? _shuttleEntity;

    /// <summary>
    /// When the next FTL state change happens.
    /// </summary>
    private TimeSpan _nextFtlTime;

    private StyleBoxFlat _ftlStyle;

    public MapScreen()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _xformSystem = _entManager.System<SharedTransformSystem>();

        MapRebuildButton.OnPressed += MapRebuildPressed;

        OnVisibilityChanged += OnVisChange;

        MapFTLButton.OnToggled += FtlPreviewToggled;

        _ftlStyle = new StyleBoxFlat(Color.LimeGreen);
        FTLBar.ForegroundStyleBoxOverride = _ftlStyle;
    }

    public void UpdateState(ShuttleConsoleBoundInterfaceState state)
    {
        var ftlState = state.FTLState;
        _nextFtlTime = state.FTLTime;

        switch (ftlState)
        {
            case FTLState.Arriving:
                _ftlStyle.BackgroundColor = Color.Goldenrod;
                break;
            case FTLState.Starting:
                _ftlStyle.BackgroundColor = Color.Blue;
                break;
            case FTLState.Travelling:
                _ftlStyle.BackgroundColor = Color.Gold;
                break;
            case FTLState.Available:
                _ftlStyle.BackgroundColor = Color.LimeGreen;
                break;
            case FTLState.Cooldown:
                _ftlStyle.BackgroundColor = Color.Red;
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private void FtlPreviewToggled(BaseButton.ButtonToggledEventArgs obj)
    {
        MapRadar.FtlMode = obj.Pressed;
    }

    public void SetShuttle(EntityUid? shuttle)
    {
        _shuttleEntity = shuttle;
        MapRadar.SetShuttle(shuttle);
    }

    private void OnVisChange(Control obj)
    {
        if (obj.Visible)
        {
            // Centre map screen to the shuttle.
            if (_shuttleEntity != null)
            {
                var mapPos = _xformSystem.GetMapCoordinates(_shuttleEntity.Value);
                MapRadar.SetMap(mapPos.MapId, mapPos.Position);
            }

            BuildMapObjects();
        }
    }

    private void MapRebuildPressed(BaseButton.ButtonEventArgs obj)
    {
        BuildMapObjects();
    }

    private void BuildMapObjects()
    {
        HyperspaceDestinations.DisposeAllChildren();
        var mapComps = _entManager.AllEntityQueryEnumerator<MapComponent>();

        while (mapComps.MoveNext(out var mapUid, out var mapComp))
        {
            var mapName = _entManager.GetComponent<MetaDataComponent>(mapUid).EntityName;

            var heading = new CollapsibleHeading(mapName);

            heading.MinHeight = 32f;
            heading.AddStyleClass(ContainerButton.StyleClassButton);
            heading.HorizontalAlignment = HAlignment.Stretch;
            heading.Label.HorizontalAlignment = HAlignment.Center;
            heading.Label.HorizontalExpand = true;
            heading.HorizontalExpand = true;

            var gridContents = new BoxContainer()
            {
                Orientation = LayoutOrientation.Vertical,
                VerticalExpand = true,
            };

            var body = new CollapsibleBody()
            {
                HorizontalAlignment = HAlignment.Stretch,
                VerticalAlignment = VAlignment.Top,
                HorizontalExpand = true,
                Children =
                {
                    gridContents
                }
            };

            var mapButton = new Collapsible(heading, body);

            heading.OnToggled += args =>
            {
                if (args.Pressed)
                {
                    HideOtherCollapsibles(mapButton);
                }
            };

            foreach (var grid in _mapManager.GetAllMapGrids(mapComp.MapId))
            {
                var gridButton = new Button()
                {
                    Text = _entManager.GetComponent<MetaDataComponent>(grid.Owner).EntityName,
                    HorizontalExpand = true,
                    MinHeight = 32f,
                };

                var gridContainer = new BoxContainer()
                {
                    Children =
                    {
                        new Control()
                        {
                            MinWidth = 32f,
                        },
                        gridButton
                    }
                };

                gridContents.AddChild(gridContainer);

                gridButton.OnPressed += args =>
                {
                    OnGridPress(grid.Owner);
                };
            }

            HyperspaceDestinations.AddChild(mapButton);

            // Zoom in to our map
            if (mapComp.MapId == MapRadar.ViewingMap)
            {
                mapButton.BodyVisible = true;
            }
        }
    }

    private void HideOtherCollapsibles(Collapsible collapsible)
    {
        foreach (var child in HyperspaceDestinations.Children)
        {
            if (child is not Collapsible childCollapse || childCollapse == collapsible)
                continue;

            childCollapse.BodyVisible = false;
        }
    }

    private void OnGridPress(EntityUid gridUid)
    {
        var mapPos = _xformSystem.GetMapCoordinates(gridUid);

        // If it's our map then scroll, otherwise just set position there.
        if (MapRadar.ViewingMap == mapPos.MapId)
        {
            MapRadar.TargetOffset = mapPos.Position;
            MapRadar.ForceRecenter();
        }
        else
        {
            MapRadar.SetMap(mapPos.MapId, mapPos.Position);
        }
    }

    public void SetMap(MapId mapId, Vector2 position)
    {
        MapRadar.SetMap(mapId, position);
    }
}
