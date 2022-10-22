using System;
using System.IO;
using System.Threading.Tasks;
using Content.Client.Interactable;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Robust.Client.Audio.Midi;
using Robust.Client.AutoGenerated;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BaseButton;
using Range = Robust.Client.UserInterface.Controls.Range;

namespace Content.Client.Instruments.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class InstrumentMenu : DefaultWindow
    {
        [Dependency] private readonly IMidiManager _midiManager = default!;
        [Dependency] private readonly IFileDialogManager _fileDialogManager = default!;

        private readonly InstrumentBoundUserInterface _owner;

        public InstrumentMenu(InstrumentBoundUserInterface owner)
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            _owner = owner;

            if (_owner.Instrument != null)
            {
                _owner.Instrument.OnMidiPlaybackEnded += InstrumentOnMidiPlaybackEnded;
                Title = IoCManager.Resolve<IEntityManager>().GetComponent<MetaDataComponent>(_owner.Instrument.Owner).EntityName;
                LoopButton.Disabled = !_owner.Instrument.IsMidiOpen;
                LoopButton.Pressed = _owner.Instrument.LoopMidi;
                StopButton.Disabled = !_owner.Instrument.IsMidiOpen;
                PlaybackSlider.MouseFilter = _owner.Instrument.IsMidiOpen ? MouseFilterMode.Pass : MouseFilterMode.Ignore;
            }

            if (!_midiManager.IsAvailable)
            {
                UnavailableOverlay.Visible = true;
                // We return early as to not give the buttons behavior.
                return;
            }

            InputButton.OnToggled += MidiInputButtonOnOnToggled;
            FileButton.OnPressed += MidiFileButtonOnOnPressed;
            LoopButton.OnToggled += MidiLoopButtonOnOnToggled;
            StopButton.OnPressed += MidiStopButtonOnPressed;
            PlaybackSlider.OnValueChanged += PlaybackSliderSeek;
            PlaybackSlider.OnKeyBindUp += PlaybackSliderKeyUp;

            MinSize = SetSize = (400, 150);
        }

        private void InstrumentOnMidiPlaybackEnded()
        {
            MidiPlaybackSetButtonsDisabled(true);
        }

        public void MidiPlaybackSetButtonsDisabled(bool disabled)
        {
            LoopButton.Disabled = disabled;
            StopButton.Disabled = disabled;

            // Whether to allow the slider to receive events..
            PlaybackSlider.MouseFilter = !disabled ? MouseFilterMode.Pass : MouseFilterMode.Ignore;
        }

        private async void MidiFileButtonOnOnPressed(ButtonEventArgs obj)
        {
            var filters = new FileDialogFilters(new FileDialogFilters.Group("mid", "midi"));
            await using var file = await _fileDialogManager.OpenFile(filters);

            // did the instrument menu get closed while waiting for the user to select a file?
            if (Disposed)
                return;

            // The following checks are only in place to prevent players from playing MIDI songs locally.
            // There are equivalents for these checks on the server.

            if (file == null) return;

            /*if (!_midiManager.IsMidiFile(filename))
            {
                Logger.Warning($"Not a midi file! Chosen file: {filename}");
                return;
            }*/

            if (!PlayCheck())
                return;

            MidiStopButtonOnPressed(null);
            await using var memStream = new MemoryStream((int) file.Length);
            // 100ms delay is due to a race condition or something idk.
            // While we're waiting, load it into memory.
            await Task.WhenAll(Timer.Delay(100), file.CopyToAsync(memStream));

            if (_owner.Instrument is not {} instrument
                || !IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InstrumentSystem>()
                    .OpenMidi(instrument.Owner, memStream.GetBuffer().AsSpan(0, (int) memStream.Length), instrument))
                return;

            MidiPlaybackSetButtonsDisabled(false);
            if (InputButton.Pressed)
                InputButton.Pressed = false;
        }

        private void MidiInputButtonOnOnToggled(ButtonToggledEventArgs obj)
        {
            var instrumentSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InstrumentSystem>();

            if (obj.Pressed)
            {
                if (!PlayCheck())
                    return;

                MidiStopButtonOnPressed(null);
                if(_owner.Instrument is {} instrument)
                    instrumentSystem.OpenInput(instrument.Owner, instrument);
            }
            else  if(_owner.Instrument is {} instrument)
                instrumentSystem.CloseInput(instrument.Owner, false, instrument);
        }

        private bool PlayCheck()
        {
            // TODO all of these checks should also be done server-side.

            var instrumentEnt = _owner.Instrument?.Owner;
            var instrument = _owner.Instrument;

            // If either the entity or component are null, return.
            if (instrumentEnt == null || instrument == null)
                return false;

            var localPlayer = IoCManager.Resolve<IPlayerManager>().LocalPlayer;

            // If we don't have a player or controlled entity, we return.
            if (localPlayer?.ControlledEntity == null) return false;

            // By default, allow an instrument to play itself and skip all other checks
            if (localPlayer.ControlledEntity == instrumentEnt)
                return true;

            // If we're a handheld instrument, we might be in a container. Get it just in case.
            instrumentEnt.Value.TryGetContainerMan(out var conMan);

            // If the instrument is handheld and we're not holding it, we return.
            if ((instrument.Handheld && (conMan == null
                                         || conMan.Owner != localPlayer.ControlledEntity))) return false;

            var entSysMan = IoCManager.Resolve<IEntitySystemManager>();
            if (!entSysMan.GetEntitySystem<ActionBlockerSystem>().CanInteract(localPlayer.ControlledEntity.Value, instrumentEnt))
                return false;

            // We check that we're in range unobstructed just in case.
            return entSysMan.GetEntitySystem<SharedInteractionSystem>().InRangeUnobstructed(localPlayer.ControlledEntity.Value, instrumentEnt.Value);
        }

        private void MidiStopButtonOnPressed(ButtonEventArgs? obj)
        {
            MidiPlaybackSetButtonsDisabled(true);

            if (_owner.Instrument is not { } instrument)
                return;

            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InstrumentSystem>().CloseMidi(instrument.Owner, false, instrument);
        }

        private void MidiLoopButtonOnOnToggled(ButtonToggledEventArgs obj)
        {
            if (_owner.Instrument == null)
                return;

            _owner.Instrument.LoopMidi = obj.Pressed;
            _owner.Instrument.DirtyRenderer = true;
        }

        private void PlaybackSliderSeek(Range _)
        {
            // Do not seek while still grabbing.
            if (PlaybackSlider.Grabbed || _owner.Instrument is not {} instrument) return;

            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InstrumentSystem>()
                .SetPlayerTick(instrument.Owner, (int)Math.Ceiling(PlaybackSlider.Value), instrument);
        }

        private void PlaybackSliderKeyUp(GUIBoundKeyEventArgs args)
        {
            if (args.Function != EngineKeyFunctions.UIClick || _owner.Instrument is not {} instrument) return;

            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InstrumentSystem>()
                .SetPlayerTick(instrument.Owner, (int)Math.Ceiling(PlaybackSlider.Value), instrument);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (_owner.Instrument == null) return;

            if (!_owner.Instrument.IsMidiOpen)
            {
                PlaybackSlider.MaxValue = 1;
                PlaybackSlider.SetValueWithoutEvent(0);
                return;
            }

            if (PlaybackSlider.Grabbed) return;

            PlaybackSlider.MaxValue = _owner.Instrument.PlayerTotalTick;
            PlaybackSlider.SetValueWithoutEvent(_owner.Instrument.PlayerTick);
        }
    }
}
