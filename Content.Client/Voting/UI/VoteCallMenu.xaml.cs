using System;
using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Administration;
using Content.Shared.Voting;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using YamlDotNet.Core.Tokens;
using static System.Net.Mime.MediaTypeNames;

namespace Content.Client.Voting.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class VoteCallMenu : BaseWindow
    {
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IVoteManager _voteManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IClientNetManager _netManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private VotingSystem _votingSystem;

        public StandardVoteType Type;

        public Dictionary<StandardVoteType, CreateVoteOption> AvailableVoteOptions = new Dictionary<StandardVoteType, CreateVoteOption>()
        {
            { StandardVoteType.Restart, new CreateVoteOption("ui-vote-type-restart", new(), false) },
            { StandardVoteType.Preset, new CreateVoteOption("ui-vote-type-gamemode", new(), false) },
            { StandardVoteType.Map, new CreateVoteOption("ui-vote-type-map", new(), false) },
            { StandardVoteType.Votekick, new CreateVoteOption("Votekick (Loc required)", new(), false) }
        };

        public Dictionary<string, string> VotekickReasons = new Dictionary<string, string>()
        {
            { VotekickReasonType.Raiding.ToString(), "Raiding (Loc required)" },
            { VotekickReasonType.Cheating.ToString(), "Cheating (Loc required)" },
            { VotekickReasonType.Spam.ToString(), "Spam (Loc required)" }
        };


        public Dictionary<string, string> PlayerList = new();

        public VoteCallMenu()
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);
            _votingSystem = _entityManager.System<VotingSystem>();

            Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSpace;
            CloseButton.OnPressed += _ => Close();


            foreach (StandardVoteType voteType in Enum.GetValues<StandardVoteType>())
            {
                var option = AvailableVoteOptions[voteType];
                VoteTypeButton.AddItem(Loc.GetString(option.Name), (int)voteType);
            }

            VoteTypeButton.OnItemSelected += VoteTypeSelected;
            CreateButton.OnPressed += CreatePressed;
        }

        protected override void Opened()
        {
            base.Opened();

            _netManager.ClientSendMessage(new MsgVoteMenu());

            _voteManager.CanCallVoteChanged += CanCallVoteChanged;
            _votingSystem.VotePlayerListResponse += UpdateVotePlayerList;
            _votingSystem.RequestVotePlayerList();
        }

        public override void Close()
        {
            base.Close();

            _voteManager.CanCallVoteChanged -= CanCallVoteChanged;
            _votingSystem.VotePlayerListResponse -= UpdateVotePlayerList;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            UpdateVoteTimeout();
        }

        private void CanCallVoteChanged(bool obj)
        {
            if (!obj)
                Close();
        }

        private void UpdateVotePlayerList(VotePlayerListResponseEvent msg)
        {
            Dictionary<string, string> list = new();
            foreach ((NetUserId, string) player in msg.Players)
            {
                list.Add(player.Item1.ToString(), player.Item2);
            }
            if (list.Count == 0)
                list.Add(" ", " ");
            PlayerList = list;

            List<Dictionary<string, string>> dropdowns = new List<Dictionary<string, string>>() { PlayerList, VotekickReasons };
            AvailableVoteOptions[StandardVoteType.Votekick].UpdateDropdowns(dropdowns);
        }

        private void GetVotekickReasons(OptionButton button)
        {
            int i = 0;
            foreach (var (key, value) in VotekickReasons)
            {
                button.AddItem(Loc.GetString(value), i);
            }
        }

        private void CreatePressed(BaseButton.ButtonEventArgs obj)
        {
            var typeId = VoteTypeButton.SelectedId;
            var voteType = AvailableVoteOptions[(StandardVoteType)typeId];

            var commandArgs = "";

            if (voteType.Dropdowns == null || voteType.Dropdowns.Count == 0)
            {
                _consoleHost.LocalShell.RemoteExecuteCommand($"createvote {((StandardVoteType)typeId).ToString()}");
            }
            else
            {
                int i = 0;
                foreach(var dropdowns in VoteOptionsButtonContainer.Children)
                {
                    if (dropdowns is OptionButton optionButton && AvailableVoteOptions[(StandardVoteType)typeId].Dropdowns != null)
                    {
                        commandArgs += " " + AvailableVoteOptions[(StandardVoteType)typeId].Dropdowns[i].ElementAt(optionButton.SelectedId);
                        i++;
                    }
                }
                _consoleHost.LocalShell.RemoteExecuteCommand($"createvote {((StandardVoteType)typeId).ToString()} {commandArgs}");
            }

            Close();
        }

        private void UpdateVoteTimeout()
        {
            var typeKey = (StandardVoteType)VoteTypeButton.SelectedId;
            var isAvailable = _voteManager.CanCallStandardVote(typeKey, out var timeout);
            //CreateButton.Disabled = !isAvailable;
            VoteTypeTimeoutLabel.Visible = !isAvailable;

            if (!isAvailable)
            {
                if (timeout == TimeSpan.Zero)
                {
                    VoteTypeTimeoutLabel.Text = Loc.GetString("ui-vote-type-not-available");
                }
                else
                {
                    var remaining = timeout - _gameTiming.RealTime;
                    VoteTypeTimeoutLabel.Text = Loc.GetString("ui-vote-type-timeout", ("remaining", remaining.ToString("mm\\:ss")));
                }
            }
        }
        private static void ButtonSelected(OptionButton.ItemSelectedEventArgs obj)
        {
            obj.Button.SelectId(obj.Id);
        }

        private void VoteTypeSelected(OptionButton.ItemSelectedEventArgs obj)
        {
            VoteTypeButton.SelectId(obj.Id);

            if (obj.Id == 3) // TODO: This whole thing needes a more elegant way of doing things. Can't be shipped looking like this! Also it breaks stuff
            {
                if (!_votingSystem.CheckVotekickInitEligibility())
                {
                    //return;
                }
            }

            var voteList = AvailableVoteOptions[(StandardVoteType)obj.Id].Dropdowns;

            VoteOptionsButtonContainer.RemoveAllChildren();
            if (voteList != null)
            {
                foreach (var voteDropdown in voteList)
                {
                    var optionButton = new OptionButton();
                    int i = 0;
                    foreach (var (key, value) in voteDropdown)
                    {
                        optionButton.AddItem(Loc.GetString(value), i);
                    }
                    VoteOptionsButtonContainer.AddChild(optionButton);
                    optionButton.OnItemSelected += ButtonSelected;
                }
            }

            VoteWarningLabel.Visible = AvailableVoteOptions[(StandardVoteType)obj.Id].EnableVoteWarning;
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            return DragMode.Move;
        }
    }

    [UsedImplicitly, AnyCommand]
    public sealed class VoteMenuCommand : IConsoleCommand
    {
        public string Command => "votemenu";
        public string Description => Loc.GetString("ui-vote-menu-command-description");
        public string Help => Loc.GetString("ui-vote-menu-command-help-text");

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            new VoteCallMenu().OpenCentered();
        }
    }

    public record struct CreateVoteOption
    {
        public string Name;
        public List<Dictionary<string, string>> Dropdowns;
        public bool EnableVoteWarning;

        public CreateVoteOption(string name, List<Dictionary<string, string>> dropdowns, bool enableVoteWarning)
        {
            Name = name;
            Dropdowns = dropdowns;
            EnableVoteWarning = enableVoteWarning;
        }

        public void UpdateDropdowns(List<Dictionary<string, string>> dropdowns)
        {
            Dropdowns = dropdowns;
        }
    }
}
