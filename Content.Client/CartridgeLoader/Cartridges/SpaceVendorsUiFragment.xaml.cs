using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client.CartridgeLoader.Cartridges;

[GenerateTypedNameReferences]
public sealed partial class SpaceVendorsUiFragment : BoxContainer
{
    private readonly IGameTiming _timing;

    private Dictionary<Label, TimeSpan> _labelsAndDateTimeCreate = new Dictionary<Label, TimeSpan>();

    private readonly StyleBoxFlat _styleBox = new()
    {
        BackgroundColor = Color.Transparent,
        BorderColor = Color.FromHex("#5a5a5a"),
        BorderThickness = new Thickness(0, 0, 0, 1)
    };

    public SpaceVendorsUiFragment()
    {
        RobustXamlLoader.Load(this);
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        HeaderPanel.PanelOverride = _styleBox;
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    public void UpdateState(List<AppraisedItem> items)
    {
        SpaceVendorsContainer.RemoveAllChildren();
        _labelsAndDateTimeCreate.Clear();

        //Reverse the list so the oldest entries appear at the bottom
        items.Reverse();

        //Enable scrolling if there are more entries that can fit on the screen
        ScrollContainer.HScrollEnabled = items.Count > 9;

        foreach (var item in items)
        {
            AddProbedDevice(item);
        }

        UpdateTimer();
    }

    private void AddProbedDevice(AppraisedItem item)
    {
        var row = new BoxContainer();
        row.HorizontalExpand = true;
        row.Orientation = LayoutOrientation.Horizontal;
        row.Margin = new Thickness(3);

        var nameLabel = new Label();
        nameLabel.Text = item.Name;
        nameLabel.HorizontalExpand = true;
        nameLabel.ClipText = true;
        row.AddChild(nameLabel);

        var priceLabel = new Label();
        priceLabel.Text = item.Price;
        priceLabel.HorizontalExpand = true;
        priceLabel.ClipText = true;
        row.AddChild(priceLabel);

        var minutesLabel = new Label();
        minutesLabel.Text = "0 min";
        minutesLabel.HorizontalExpand = true;
        minutesLabel.ClipText = true;
        row.AddChild(minutesLabel);

        _labelsAndDateTimeCreate.Add(minutesLabel,item.DateTimeCreation);

        SpaceVendorsContainer.AddChild(row);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateTimer();
    }

    private void UpdateTimer()
    {
        foreach (var label in _labelsAndDateTimeCreate)
        {
            TimeSpan time = _timing.CurTime - label.Value;
            label.Key.Text = (time.Hours * 60 + time.Minutes).ToString()+" min";
        }
    }
}
