using Content.Client.Examine;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using static Content.Client.Stylesheets.Redux.StylesheetHelpers;

namespace Content.Client.Stylesheets.Redux.Sheetlets.Hud;

[CommonSheetlet]
public sealed class ExamineButtonSheetlet : Sheetlet<PalettedStylesheet>
{
    // Examine button colors
    // TODO: FIX!!
    private static readonly Color ExamineButtonColorContext = Color.Transparent;
    private static readonly Color ExamineButtonColorContextHover = Color.DarkSlateGray;
    private static readonly Color ExamineButtonColorContextPressed = Color.LightSlateGray;
    private static readonly Color ExamineButtonColorContextDisabled = Color.FromHex("#5A5A5A");

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var buttonContext = new StyleBoxTexture { Texture = Texture.White };

        return
        [
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonContext),
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .ButtonNormal()
                .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContext),
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .ButtonHovered()
                .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextHover),
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .ButtonPressed()
                .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextPressed),
            E<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .ButtonDisabled()
                .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextDisabled),
        ];
    }
}
