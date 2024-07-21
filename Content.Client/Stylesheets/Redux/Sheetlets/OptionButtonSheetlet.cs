using Content.Client.Stylesheets.Redux.SheetletConfigs;
using Content.Client.Stylesheets.Redux.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.Redux.StylesheetHelpers;

namespace Content.Client.Stylesheets.Redux.Sheetlets;

[CommonSheetlet]
public sealed class OptionButtonSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var iconCfg = (IIconConfig) sheet;

        var invertedTriangleTex =
            sheet.GetTextureOr(iconCfg.InvertedTriangleIconPath, NanotrasenStylesheet.TextureRoot);

        return
        [
            E<TextureRect>()
                .Class(OptionButton.StyleClassOptionTriangle)
                .Prop(TextureRect.StylePropertyTexture, invertedTriangleTex),
            E<Label>().Class(OptionButton.StyleClassOptionButton).AlignMode(Label.AlignMode.Center),
        ];
    }
}
