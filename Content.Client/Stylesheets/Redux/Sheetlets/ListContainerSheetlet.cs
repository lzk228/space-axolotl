using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.Redux.StylesheetHelpers;

namespace Content.Client.Stylesheets.Redux.Sheetlets;

[CommonSheetlet]
public sealed class ListContainerSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        // TODO: why is this hardcoded???
        var box = new StyleBoxFlat() { BackgroundColor = Color.White };

        return
        [
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .Box(box),
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .PseudoNormal()
                .Modulate(new Color(55, 55, 68)),
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .PseudoHovered()
                .Modulate(new Color(75, 75, 86)),
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .PseudoPressed()
                .Modulate(new Color(75, 75, 86)),
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .PseudoDisabled()
                .Modulate(new Color(10, 10, 12)),
        ];
    }
}
