using Content.Shared.MachineLinking;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Graphics;

namespace Content.Client.MachineLinking.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class SignalPortSelectorMenu : DefaultWindow
    {
        private SignalPortSelectorBoundUserInterface _bui;
        private LinksRender links;

        private ButtonGroup buttonGroup = new();

        public SignalPortSelectorMenu(SignalPortSelectorBoundUserInterface boundUserInterface)
        {
            RobustXamlLoader.Load(this);
            _bui = boundUserInterface;
            links = new(ButtonContainerLeft, ButtonContainerRight);
            ContainerMiddle.AddChild(links);
            ButtonClear.OnPressed += _ => _bui.OnClearPressed();
            ButtonLinkDefault.OnPressed += _ => _bui.OnLinkDefaultPressed();
        }

        public void UpdateState(SignalPortsState state)
        {
            HeaderLeft.SetMessage(state.TransmitterName);
            ButtonContainerLeft.DisposeAllChildren();
            foreach (var port in state.TransmitterPorts)
            {
                var portButton = new Button() { Text = port, ToggleMode = true, Group = buttonGroup };
                portButton.OnPressed += _ => _bui.OnTransmitterPortSelected(port);
                ButtonContainerLeft.AddChild(portButton);
            }

            HeaderRight.SetMessage(state.ReceiverName);
            ButtonContainerRight.DisposeAllChildren();
            foreach (var port in state.ReceiverPorts)
            {
                var portButton = new Button() { Text = port, ToggleMode = true, Group = buttonGroup };
                portButton.OnPressed += _ => _bui.OnReceiverPortSelected(port);
                ButtonContainerRight.AddChild(portButton);
            }

            links.Links = state.Links;
        }

        private sealed class LinksRender : Control
        {
            public List<(int, int)> Links = new();
            public BoxContainer LeftButton;
            public BoxContainer RightButton;

            public LinksRender(BoxContainer leftButton, BoxContainer rightButton)
            {
                LeftButton = leftButton;
                RightButton = rightButton;
            }

            protected override void Draw(DrawingHandleScreen handle)
            {
                var leftOffset = LeftButton.PixelPosition.Y;
                var rightOffset = RightButton.PixelPosition.Y;
                foreach (var (left, right) in Links)
                {
                    var leftChild = LeftButton.GetChild(left);
                    var rightChild = RightButton.GetChild(right);
                    var y1 = leftChild.PixelPosition.Y + leftChild.PixelHeight / 2 + leftOffset;
                    var y2 = rightChild.PixelPosition.Y + rightChild.PixelHeight / 2 + rightOffset;
                    handle.DrawLine((0, y1), (PixelWidth, y2), Color.Cyan);
                }
            }
        }
    }
}
