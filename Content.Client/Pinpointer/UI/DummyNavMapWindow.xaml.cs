using Content.Client.UserInterface.Controls;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Pinpointer.UI;

[GenerateTypedNameReferences]
public sealed partial class DummyNavMapWindow : FancyWindow
{
    public DummyNavMapWindow(EntityUid? uid)
    {
        RobustXamlLoader.Load(this);
        NavMapScreen.Uid = uid;
    }
}
