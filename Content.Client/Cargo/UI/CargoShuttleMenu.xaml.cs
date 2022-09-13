using Content.Client.UserInterface.Controls;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Cargo.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class CargoShuttleMenu : FancyWindow
    {
        private readonly IGameTiming _timing;
        private readonly IPrototypeManager _protoManager;
        private readonly SpriteSystem _spriteSystem;

        public Action? ShuttleCallRequested;
        public Action? ShuttleRecallRequested;

        private TimeSpan? _shuttleEta;

        public CargoShuttleMenu(IGameTiming timing, IPrototypeManager protoManager, SpriteSystem spriteSystem)
        {
            RobustXamlLoader.Load(this);
            _timing = timing;
            _protoManager = protoManager;
            _spriteSystem = spriteSystem;
            ShuttleCallButton.OnPressed += OnCallPressed;
            ShuttleRecallButton.OnPressed += OnRecallPressed;
            Title = Loc.GetString("cargo-shuttle-console-menu-title");
        }

        public void SetAccountName(string name)
        {
            AccountNameLabel.Text = name;
        }

        public void SetShuttleName(string name)
        {
            ShuttleNameLabel.Text = name;
        }

        public void SetShuttleETA(TimeSpan? eta)
        {
            _shuttleEta = eta;

            if (eta == null)
            {
                ShuttleCallButton.Visible = false;
                ShuttleRecallButton.Visible = true;
            }
            else
            {
                ShuttleRecallButton.Visible = false;
                ShuttleCallButton.Visible = true;
                ShuttleCallButton.Disabled = true;
            }
        }

        private void OnRecallPressed(BaseButton.ButtonEventArgs obj)
        {
            ShuttleRecallRequested?.Invoke();
        }

        private void OnCallPressed(BaseButton.ButtonEventArgs obj)
        {
            ShuttleCallRequested?.Invoke();
        }

        public void SetOrders(List<CargoOrderData> orders)
        {
            Orders.DisposeAllChildren();

            foreach (var order in orders)
            {
                 var product = _protoManager.Index<CargoProductPrototype>(order.ProductId);
                 var productName = product.Name;

                 var row = new CargoOrderRow
                 {
                     Order = order,
                     Icon = { Texture = _spriteSystem.Frame0(product.Icon) },
                     ProductName =
                     {
                         Text = Loc.GetString(
                             "cargo-console-menu-populate-orders-cargo-order-row-product-name-text",
                             ("productName", productName),
                             ("orderAmount", order.Amount),
                             ("orderRequester", order.Requester))
                     },
                     Description = {Text = Loc.GetString("cargo-console-menu-order-reason-description",
                         ("reason", order.Reason))}
                 };

                 row.Approve.Visible = false;
                 row.Cancel.Visible = false;

                 Orders.AddChild(row);
            }
        }

        public void SetCanRecall(bool canRecall)
        {
            ShuttleRecallButton.Disabled = !canRecall;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            var remaining = _shuttleEta - _timing.CurTime;

            if (remaining == null || remaining <= TimeSpan.Zero)
            {
                ShuttleStatusLabel.Text = $"Available";
                ShuttleCallButton.Disabled = false;
            }
            else
            {
                ShuttleStatusLabel.Text = $"Available in: {remaining.Value.TotalSeconds:0.0}";
            }
        }
    }
}
