using Robust.Client.UserInterface.CustomControls;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Access.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class AgentIDCardWindow : DefaultWindow
    {
        public event Action<string>? OnNameChanged;
        public event Action<string>? OnJobChanged;

        public AgentIDCardWindow()
        {
            RobustXamlLoader.Load(this);

            NameLineEdit.OnTextEntered += e => OnNameChanged?.Invoke(e.Text);
            NameLineEdit.OnFocusExit += e => OnNameChanged?.Invoke(e.Text);

            JobLineEdit.OnTextEntered += e => OnJobChanged?.Invoke(e.Text);
            JobLineEdit.OnFocusExit += e => OnJobChanged?.Invoke(e.Text);
        }

        public void SetCurrentName(string name)
        {
            NameLineEdit.Text = name;
        }

        public void SetCurrentJob(string job)
        {
            JobLineEdit.Text = job;
        }
    }
}
