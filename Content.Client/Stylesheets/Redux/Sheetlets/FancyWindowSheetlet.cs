﻿using System.Numerics;
using Content.Client.Stylesheets.Redux.Fonts;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Content.Client.Stylesheets.Redux.StylesheetHelpers;

namespace Content.Client.Stylesheets.Redux.Sheetlets;

[CommonSheetlet]
public sealed class FancyWindowSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var boxFont = new SingleFont(ResCache, "/Fonts/Boxfont-round/Boxfont Round.ttf");
        return
        [
            // Title
            E<Label>()
                .Class("FancyWindowTitle")
                .Prop("font", boxFont.GetFont(13, FontStack.FontKind.Bold))
                .Prop("font-color", sheet.HighlightPalette.Text),

            // Help Button
            E<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Prop(TextureButton.StylePropertyTexture, sheet.GetTexture("help.png"))
                .Prop(Control.StylePropertyModulateSelf, sheet.PrimaryPalette.Element),
            E<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, sheet.PrimaryPalette.HoveredElement),
            E<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, sheet.PrimaryPalette.PressedElement),

            // Close Button
            E<TextureButton>()
                .Class(FancyWindow.StyleClassWindowCloseButton)
                .Margin(new Thickness(0, 0, -3, 0)),

            // Footer
            E<Label>()
                .Class("WindowFooterText")
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(8))
                .Prop(Label.StylePropertyFontColor, Color.FromHex("#757575")),
        ];
    }
}
