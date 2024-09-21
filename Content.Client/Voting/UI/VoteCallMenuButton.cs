using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client.Voting.UI
{
    /// <summary>
    ///     LITERALLY just a button that opens the vote call menu.
    ///     Automatically disables itself if the client cannot call votes.
    /// </summary>
    public sealed class VoteCallMenuButton : Button
    {
        [Dependency] private readonly IVoteManager _voteManager = default!;

        private VoteCallMenu? _voteCallMenu;

        public VoteCallMenuButton()
        {
            IoCManager.InjectDependencies(this);

            Text = Loc.GetString("ui-vote-menu-button");
            OnPressed += OnOnPressed;
        }

        private void OnOnPressed(ButtonEventArgs obj)
        {
            if (_voteCallMenu is { IsOpen: true })
            {
                _voteCallMenu.Close();

                return;
            }

            _voteCallMenu = new VoteCallMenu();

            _voteCallMenu.OnClose += () => Pressed = false;

            _voteCallMenu.OpenCentered();
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();

            UpdateCanCall(_voteManager.CanCallVote);
            _voteManager.CanCallVoteChanged += UpdateCanCall;
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            _voteManager.CanCallVoteChanged += UpdateCanCall;

            if (_voteCallMenu is { IsOpen: true })
            {
                _voteCallMenu.Close();
            }
        }

        private void UpdateCanCall(bool canCall)
        {
            Disabled = !canCall;
        }
    }
}
