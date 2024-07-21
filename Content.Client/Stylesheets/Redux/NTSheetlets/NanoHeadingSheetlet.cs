using Content.Client.Stylesheets.Redux.SheetletConfigs;
using Content.Client.Stylesheets.Redux.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.Redux.StylesheetHelpers;

namespace Content.Client.Stylesheets.Redux.NTSheetlets;

/// Not NTHeading because NanoHeading is the name of the element
[CommonSheetlet]
public sealed class NanoHeadingSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        var nanoHeadingCfg = (INanoHeadingConfig) sheet;

        var nanoHeadingTex = sheet.GetTexture(nanoHeadingCfg.NanoHeadingPath);
        var nanoHeadingBox = new StyleBoxTexture
        {
            Texture = nanoHeadingTex,
            PatchMarginRight = 10,
            PatchMarginTop = 10,
            ContentMarginTopOverride = 2,
            ContentMarginLeftOverride = 10,
            PaddingTop = 4,
        };
        nanoHeadingBox.SetPatchMargin(StyleBox.Margin.Left | StyleBox.Margin.Bottom, 2);

        return
        [
            E<NanoHeading>().ParentOf(E<PanelContainer>()).Panel(nanoHeadingBox),
        ];
    }
}
