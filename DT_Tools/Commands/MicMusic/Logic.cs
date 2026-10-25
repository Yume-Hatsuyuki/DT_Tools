using DT_Tools.Game;

namespace DT_Tools.Commands.MicMusic
{
    internal static class MicMusicLogic
    {
        public static bool Execute(MicMusicArgs args)
        {
            if (args.Off)
            {
                MicBroadcast.Stop();
                return true;
            }
            return MicBroadcast.Play(args.Source, args.SourceKind, args.Volume, args.MaxSeconds);
        }
    }
}
