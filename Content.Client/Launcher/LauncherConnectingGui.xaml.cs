using System.Linq;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.Dataset;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Launcher
{
    [GenerateTypedNameReferences]
    public sealed partial class LauncherConnectingGui : Control
    {
        private const float RedialWaitTimeSeconds = 15f;
        private readonly LauncherConnecting _state;
        private float _waitTime;

        // Pressing reconnect will redial instead of simply reconnecting.
        private bool _redial;

        [Dependency] private readonly IClipboardManager _clipboard = null!;
        private readonly IRobustRandom _random;
        private readonly IPrototypeManager _prototype;
        private readonly IConfigurationManager _cfg;

        public LauncherConnectingGui(LauncherConnecting state, IRobustRandom random,
            IPrototypeManager prototype, IConfigurationManager config)
        {
            _state = state;
            _random = random;
            _prototype = prototype;
            _cfg = config;

            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);

            Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSpace;

            ChangeLoginTip();
            RetryButton.OnPressed += ReconnectButtonPressed;
            ReconnectButton.OnPressed += ReconnectButtonPressed;

            CopyButton.OnPressed += CopyButtonPressed;
            CopyButtonDisconnected.OnPressed += CopyButtonDisconnectedPressed;
            ExitButton.OnPressed += _ => _state.Exit();

            var addr = state.Address;
            if (addr != null)
                ConnectingAddress.Text = addr;

            state.PageChanged += OnPageChanged;
            state.ConnectFailReasonChanged += ConnectFailReasonChanged;
            state.ConnectionStateChanged += ConnectionStateChanged;
            state.ConnectFailed += HandleDisconnectReason;

            ConnectionStateChanged(state.ConnectionState);

            // Redial flag setup
            var edim = IoCManager.Resolve<ExtendedDisconnectInformationManager>();
            edim.LastNetDisconnectedArgsChanged += LastNetDisconnectedArgsChanged;
            LastNetDisconnectedArgsChanged(edim.LastNetDisconnectedArgs);
        }

        // Just button, there's only one at once anyways :)
        private void ReconnectButtonPressed(BaseButton.ButtonEventArgs args)
        {
            if (_redial)
            {
                // Redial shouldn't fail, but if it does, try a reconnect (maybe we're being run from debug)
                if (_state.Redial())
                    return;
            }

            _state.RetryConnect();
        }

        private void CopyButtonPressed(BaseButton.ButtonEventArgs args)
        {
            CopyText(ConnectFailReason.Text);
        }

        private void CopyButtonDisconnectedPressed(BaseButton.ButtonEventArgs args)
        {
            CopyText(DisconnectReason.Text);
        }

        private void CopyText(string? text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _clipboard.SetText(text);
            }
        }

        private void ConnectFailReasonChanged(string? reason)
        {
            ConnectFailReason.SetMessage(reason == null
                ? ""
                : Loc.GetString("connecting-fail-reason", ("reason", reason)));
        }

        private void LastNetDisconnectedArgsChanged(NetDisconnectedArgs? args)
        {
            HandleDisconnectReason(args);
        }

        private void HandleDisconnectReason(INetStructuredReason? reason)
        {
            if (reason == null)
            {
                _waitTime = 0;
                _redial = false;
            }
            else
            {
                _redial = reason.RedialFlag;

                if (reason.Message.Int32Of("delay") is { } delay)
                {
                    _waitTime = delay;
                }
                else if (_redial)
                {
                    _waitTime = RedialWaitTimeSeconds;
                }

            }
        }

        private void ChangeLoginTip()
        {
            var tipsDataset = _cfg.GetCVar(CCVars.LoginTipsDataset);
            var loginTipsEnabled = _prototype.TryIndex<LocalizedDatasetPrototype>(tipsDataset, out var tips);

            LoginTips.Visible = loginTipsEnabled;
            if (!loginTipsEnabled)
            {
                return;
            }

            var tipList = tips!.Values;

            if (tipList.Count == 0)
                return;

            var randomIndex = _random.Next(tipList.Count);
            var tip = tipList[randomIndex];
            LoginTip.SetMessage(Loc.GetString(tip));

            LoginTipTitle.Text = Loc.GetString("connecting-window-tip", ("numberTip", randomIndex));
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            var button = _state.CurrentPage == LauncherConnecting.Page.ConnectFailed
                ? RetryButton
                : ReconnectButton;

            _waitTime -= args.DeltaSeconds;
            if (_waitTime <= 0)
            {
                button.Disabled = false;
                var key = _redial
                    ? "connecting-redial"
                    : _state.CurrentPage == LauncherConnecting.Page.ConnectFailed
                        ? "connecting-reconnect"
                        : "connecting-retry";

                button.Text = Loc.GetString(key);
            }
            else
            {
                button.Disabled = true;
                button.Text = Loc.GetString("connecting-redial-wait", ("time", _waitTime.ToString("00.000")));
            }
        }

        private void OnPageChanged(LauncherConnecting.Page page)
        {
            ConnectingStatus.Visible = page == LauncherConnecting.Page.Connecting;
            ConnectFail.Visible = page == LauncherConnecting.Page.ConnectFailed;
            Disconnected.Visible = page == LauncherConnecting.Page.Disconnected;

            if (page == LauncherConnecting.Page.Disconnected)
                DisconnectReason.Text = _state.LastDisconnectReason;
        }

        private void ConnectionStateChanged(ClientConnectionState state)
        {
            ConnectStatus.Text = Loc.GetString($"connecting-state-{state}");
        }
    }
}
