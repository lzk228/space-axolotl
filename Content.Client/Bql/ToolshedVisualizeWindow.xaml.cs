﻿using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Bql;

[GenerateTypedNameReferences]
internal sealed partial class ToolshedVisualizeWindow : DefaultWindow
{
    private readonly IClientConsoleHost _console;
    private readonly ILocalizationManager _loc;

    public ToolshedVisualizeWindow(IClientConsoleHost console, ILocalizationManager loc)
    {
        _console = console;
        _loc = loc;

        RobustXamlLoader.Load(this);
    }

    public void Update((string name, NetEntity entity)[] entities)
    {
        StatusLabel.Text = _loc.GetString("ui-bql-results-status", ("count", entities.Length));
        ItemList.RemoveAllChildren();

        foreach (var (name, entity) in entities)
        {
            var nameLabel = new Label { Text = name, HorizontalExpand = true };
            var tpButton = new Button { Text = _loc.GetString("ui-bql-results-tp") };
            tpButton.OnPressed += _ => _console.ExecuteCommand($"tpto {entity}");
            tpButton.ToolTip = _loc.GetString("ui-bql-results-tp-tooltip");

            var vvButton = new Button { Text = _loc.GetString("ui-bql-results-vv") };
            vvButton.ToolTip = _loc.GetString("ui-bql-results-vv-tooltip");
            vvButton.OnPressed += _ => _console.ExecuteCommand($"vv {entity}");

            ItemList.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children = { nameLabel, tpButton, vvButton }
            });
        }
    }
}
