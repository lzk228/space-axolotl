using Content.Shared.CrewManifest;
using Robust.Client.AutoGenerated;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Utility;
using Robust.Shared.Utility;

namespace Content.Client.CrewManifest;

/// <summary>
///     Crew manifest window. This is intended to be opened by other UIs, and as a result,
///     those UIs should ensure any controller registers this window with the intended
///     station it's meant to track.
/// </summary>
[GenerateTypedNameReferences]
public sealed partial class CrewManifestUi : DefaultWindow
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    private EntityUid? _station;

    public CrewManifestUi()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        StationName.AddStyleClass("LabelBig");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_station != null)
        {
            _entitySystemManager.GetEntitySystem<CrewManifestSystem>().UnsubscribeCrewManifestUpdate(_station.Value, UpdateManifest);
        }
    }

    public void Register(EntityUid station)
    {
        if (_station != null)
        {
            return;
        }

        _station = station;
        _entitySystemManager.GetEntitySystem<CrewManifestSystem>().SubscribeCrewManifestUpdate(station, UpdateManifest);
    }

    private void UpdateManifest(CrewManifestState state)
    {
        StationName.Visible = state.Station != null;
        Populate(state.Entries);
    }

    public void Populate(CrewManifestEntries? entries)
    {
        CrewManifestListing.DisposeAllChildren();
        CrewManifestListing.RemoveAllChildren();

        if (entries == null)
        {
            CrewManifestListing.AddChild(new Label()
            {
                Text = Loc.GetString("crew-manifest-no-valid-station")
            });

            return;
        }

        foreach (var (title, list) in entries.Entries)
        {
            CrewManifestListing.AddChild(new CrewManifestSection(title, list, _resourceCache));
        }
    }

    private sealed class CrewManifestSection : BoxContainer
    {
        public CrewManifestSection(string sectionTitle, List<CrewManifestEntry> entries, IResourceCache cache)
        {
            AddChild(new Label()
            {
                StyleClasses = { "LabelBig" },
                Text = Loc.GetString(sectionTitle)
            });

            entries.Sort((a, b) => b.DisplayPriority.CompareTo(a.DisplayPriority));

            var gridContainer = new GridContainer()
            {
                Columns = 2
            };

            var path = new ResourcePath("/Textures/Interface/Misc/job_icons.rsi");
            cache.TryGetResource(path, out RSIResource? rsi);

            foreach (var entry in entries)
            {
                var name = new Label()
                {
                    Text = entry.Name
                };

                var titleContainer = new BoxContainer()
                {
                    Orientation = LayoutOrientation.Horizontal
                };

                var title = new Label()
                {
                    Text = entry.JobTitle
                };


                if (rsi != null)
                {
                    var icon = new TextureRect()
                    {
                        TextureScale = (2, 2),
                        Stretch = TextureRect.StretchMode.KeepCentered
                    };

                    if (rsi.RSI.TryGetState(entry.JobIcon, out _))
                    {
                        var specifier = new SpriteSpecifier.Rsi(path, entry.JobIcon);
                        icon.Texture = specifier.Frame0();
                    }
                    else if (rsi.RSI.TryGetState("Unknown", out _))
                    {
                        var specifier = new SpriteSpecifier.Rsi(path, "Unknown");
                        icon.Texture = specifier.Frame0();
                    }

                    titleContainer.AddChild(icon);
                    titleContainer.AddChild(title);
                }
                else
                {
                    titleContainer.AddChild(title);
                }

                gridContainer.AddChild(name);
                gridContainer.AddChild(titleContainer);
            }
        }
    }
}
