using Content.Shared.Ghost.Roles;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Ghost.Roles.UI;

[GenerateTypedNameReferences]
public sealed partial class GhostRoleGroupEntry : BoxContainer
{
    public event Action<GhostRoleGroupInfo>? OnGroupSelected;
    public event Action<GhostRoleGroupInfo>? OnGroupCancelled;

    public event Action<GhostRoleGroupInfo>? OnGroupDelete;
    public event Action<GhostRoleGroupInfo>? OnGroupRelease;

    public GhostRoleGroupEntry(GhostRoleGroupInfo group, bool adminControls)
    {
        RobustXamlLoader.Load(this);

        var total = group.AvailableCount;
        var ready = group.Status == "Released";

        Title.Text = total > 1 ? $"{group.Name} ({total})" : group.Name;
        Description.SetMessage(group.Description);

        RequestButton.Text = "Request";

        RequestButton.Visible = ready && !group.IsRequested;
        CancelButton.Visible = ready && group.IsRequested;

        AdminControls.Visible = adminControls;
        ReleaseButton.Visible = group.Status == "Editing";

        RequestButton.OnPressed += _ => OnGroupSelected?.Invoke(group);
        CancelButton.OnPressed += _ => OnGroupCancelled?.Invoke(group);
        ReleaseButton.OnPressed += _ => OnGroupRelease?.Invoke(group);
        DeleteButton.OnPressed += _ => OnGroupDelete?.Invoke(group);
    }
}
