using Robust.Shared.Serialization;

namespace Content.Shared.PDA.Ringer
{
    [Serializable, NetSerializable]
    public sealed class RingerUpdateState : IBoundUserInterfaceState
    {
        public bool IsPlaying;
        public Note[] Ringtone;

        public RingerUpdateState(bool isPlay, Note[] ringtone)
        {
            IsPlaying = isPlay;
            Ringtone = ringtone;
        }
    }

}
