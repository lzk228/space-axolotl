﻿using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Administration.UI.Tabs.PlayerTab;

[GenerateTypedNameReferences]
public sealed partial class PlayerTabEntry : ContainerButton
{
    public NetEntity? PlayerEntity;

    public PlayerTabEntry(string username, string character, string identity, string job, string antagonist, StyleBox styleBox, bool connected, string overallPlaytime)
    {
        RobustXamlLoader.Load(this);

        UsernameLabel.Text = username;
        if (!connected)
            UsernameLabel.StyleClasses.Add("Disabled");
        JobLabel.Text = job;
        CharacterLabel.Text = character;
        if (identity != character)
            CharacterLabel.Text += $" [{identity}]";
        AntagonistLabel.Text = antagonist;
        BackgroundColorPanel.PanelOverride = styleBox;
        OverallPlaytimeLabel.Text = overallPlaytime;
    }
}
