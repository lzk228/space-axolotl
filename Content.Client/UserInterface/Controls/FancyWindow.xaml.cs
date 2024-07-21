﻿using System.Numerics;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Components;
using Content.Client.Stylesheets;
using Content.Shared.Guidebook;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Controls
{
    [GenerateTypedNameReferences]
    [Virtual]
    public partial class FancyWindow : BaseWindow
    {
        [Dependency] private readonly IEntitySystemManager _sysMan = default!;
        [Dependency] private readonly IStylesheetManager _styleMan = default!;
        private GuidebookSystem? _guidebookSystem;
        private const int DRAG_MARGIN_SIZE = 7;

        public const string StyleClassWindowHelpButton = "windowHelpButton";
        public const string StyleClassWindowCloseButton = "windowCloseButton";

        public FancyWindow()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            CloseButton.OnPressed += _ => Close();
            HelpButton.OnPressed += _ => Help();
            XamlChildren = ContentsContainer.Children;
        }

        public string? Title
        {
            get => WindowTitle.Text;
            set => WindowTitle.Text = value;
        }

        private string? _stylesheet;

        public new string? Stylesheet
        {
            get => _stylesheet;
            set
            {
                _stylesheet = value;
                if (value is not null && _styleMan.Stylesheets.TryGetValue(value, out var stylesheet))
                    base.Stylesheet = stylesheet;
            }
        }

        private List<ProtoId<GuideEntryPrototype>>? _helpGuidebookIds;

        public List<ProtoId<GuideEntryPrototype>>? HelpGuidebookIds
        {
            get => _helpGuidebookIds;
            set
            {
                _helpGuidebookIds = value;
                HelpButton.Disabled = _helpGuidebookIds == null;
                HelpButton.Visible = !HelpButton.Disabled;
            }
        }

        public void Help()
        {
            if (HelpGuidebookIds is null)
                return;
            _guidebookSystem ??= _sysMan.GetEntitySystem<GuidebookSystem>();
            _guidebookSystem.OpenHelp(HelpGuidebookIds);
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            var mode = DragMode.Move;

            if (Resizable)
            {
                if (relativeMousePos.Y < DRAG_MARGIN_SIZE)
                {
                    mode = DragMode.Top;
                }
                else if (relativeMousePos.Y > Size.Y - DRAG_MARGIN_SIZE)
                {
                    mode = DragMode.Bottom;
                }

                if (relativeMousePos.X < DRAG_MARGIN_SIZE)
                {
                    mode |= DragMode.Left;
                }
                else if (relativeMousePos.X > Size.X - DRAG_MARGIN_SIZE)
                {
                    mode |= DragMode.Right;
                }
            }

            return mode;
        }

        public string HeaderClass { set => WindowHeader.SetOnlyStyleClass(value); }
        public string TitleClass { set => WindowTitle.SetOnlyStyleClass(value); }
    }
}
