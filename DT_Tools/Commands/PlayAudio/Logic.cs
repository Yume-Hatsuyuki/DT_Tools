using DT_Tools.Game;

namespace DT_Tools.Commands.PlayAudio
{
    internal static class PlayAudioLogic
    {
        public static bool Execute(AudioSourceArgs args)
        {
            return AudioPlayback.Play(args.Source, args.SourceKind, args.Volume, args.MaxSeconds);
        }
    }
}
