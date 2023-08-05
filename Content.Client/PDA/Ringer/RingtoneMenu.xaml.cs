using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Content.Shared.PDA;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.PDA.Ringer
{
    [GenerateTypedNameReferences]
    public sealed partial class RingtoneMenu : DefaultWindow
    {
        public string[] PreviousNoteInputs = new[] { "A", "A", "A", "A", "A", "A" };
        public LineEdit[] RingerNoteInputs = default!;

        public RingtoneMenu()
        {
            RobustXamlLoader.Load(this);

            RingerNoteInputs = new[] { RingerNoteOneInput, RingerNoteTwoInput, RingerNoteThreeInput, RingerNoteFourInput, RingerNoteFiveInput, RingerNoteSixInput };

            for (var i = 0; i < RingerNoteInputs.Length; ++i)
            {
                var input = RingerNoteInputs[i];
                var index = i;
                var foo = () => // Prevents unauthorized characters from being entered into the LineEdit
                {
                    input.Text = input.Text.ToUpper();

                    if (!IsNote(input.Text))
                    {
                        input.Text = PreviousNoteInputs[index];
                    }
                    else
                        PreviousNoteInputs[index] = input.Text;

                    input.RemoveStyleClass("Caution");
                };

                input.OnFocusExit += _ => foo();
                input.OnTextEntered += _ =>
                {
                    foo();
                    input.CursorPosition = input.Text.Length; // Resets caret position to the end of the typed input
                };
                input.OnTextChanged += _ =>
                {
                    input.Text = input.Text.ToUpper();

                    if (!IsNote(input.Text))
                        input.AddStyleClass("Caution");
                    else
                        input.RemoveStyleClass("Caution");
                };
            }
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            //Prevents the ringtone window from being resized
            return DragMode.Move;
        }

        /// <summary>
        /// Determines whether or not the characters inputed are authorized
        /// </summary>
        public static bool IsNote(string input)
        {
            input = input.Replace("#", "sharp");

            return Enum.TryParse(input, true, out Note _);
        }
    }
}
