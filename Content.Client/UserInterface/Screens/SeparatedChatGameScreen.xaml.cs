using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.Screens;

[GenerateTypedNameReferences]
public sealed partial class SeparatedChatGameScreen : UIScreen
{
    public SeparatedChatGameScreen()
    {
        RobustXamlLoader.Load(this);
    }
}
