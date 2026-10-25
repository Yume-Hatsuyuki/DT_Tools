using DT_Tools.Game;

namespace DT_Tools.Commands.MicMusic
{
    internal sealed class MicMusicArgs
    {
        internal const float DefaultVolume = 0.85f;

        public bool Off { get; private set; }
        public AudioSourceArgs Audio { get; private set; }

        public string Source => Audio.Source;
        public AudioPlaybackSource SourceKind => Audio.SourceKind;
        public float MaxSeconds => Audio.MaxSeconds;
        public float Volume => Audio.Volume;

        public static bool TryParse(string[] tokens, out MicMusicArgs args, out string error)
        {
            args = new MicMusicArgs();
            if (tokens.Length > 0)
            {
                var first = tokens[0].ToLowerInvariant();
                if (first == "off" || first == "stop" || first == "停止")
                {
                    args.Off = true;
                    error = null;
                    return true;
                }
            }
            if (!AudioSourceArgs.TryParse(tokens, DefaultVolume, out var audio, out error))
                return false;
            args.Audio = audio;
            return true;
        }
    }
}
