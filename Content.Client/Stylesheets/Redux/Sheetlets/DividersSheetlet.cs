﻿using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.Redux.StylesheetHelpers;

namespace Content.Client.Stylesheets.Redux.Sheetlets;

[CommonSheetlet]
public sealed class DividersSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        return
        [
            E<PanelContainer>()
                .Class(StyleClass.LowDivider)
                .Panel(new StyleBoxFlat(sheet.SecondaryPalette.TextDark))
                .MinSize(new Vector2(2, 2)),
        ];
    }
}
