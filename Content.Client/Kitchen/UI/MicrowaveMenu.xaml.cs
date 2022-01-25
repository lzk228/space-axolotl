﻿using System;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Localization;

namespace Content.Client.Kitchen.UI
{
    [GenerateTypedNameReferences]
    public partial class MicrowaveMenu : DefaultWindow
    {
        public class MicrowaveCookTimeButton : Button
        {
            public uint CookTime;
        }

        public event Action<BaseButton.ButtonEventArgs, int>? OnCookTimeSelected;

        private ButtonGroup CookTimeButtonGroup { get; }

        public MicrowaveMenu(MicrowaveBoundUserInterface owner)
        {
            RobustXamlLoader.Load(this);

            CookTimeButtonGroup = new ButtonGroup();

            for (var i = 0; i <= 30; i += 5)
            {
                var newButton = new MicrowaveCookTimeButton
                {
                    Text = i == 0 ? Loc.GetString("microwave-menu-instant-button") : i.ToString(),
                    CookTime = (uint) i,
                    TextAlign = Label.AlignMode.Center,
                    ToggleMode = true,
                    Group = CookTimeButtonGroup,
                };
                CookTimeButtonVbox.AddChild(newButton);
                newButton.OnToggled += args =>
                {
                    OnCookTimeSelected?.Invoke(args, newButton.GetPositionInParent());
                };
            }
        }

        public void ToggleBusyDisableOverlayPanel(bool shouldDisable)
        {
            DisableCookingPanelOverlay.Visible = shouldDisable;
        }
    }
}
