﻿using Content.Client.UserInterface.Controls;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Xenobiology.UI;

[GenerateTypedNameReferences]
public sealed partial class CellSequencerWindow : FancyWindow
{
    public CellSequencerWindow()
    {
        RobustXamlLoader.Load(this);
    }
}
