﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.UIWindows;

[GenerateTypedNameReferences]
public sealed partial class InventoryWindow : FancyWindow
{
    public InventoryWindow()
    {
        RobustXamlLoader.Load(this);
        LayoutContainer.SetAnchorAndMarginPreset(this,LayoutContainer.LayoutPreset.Center);
    }
}
