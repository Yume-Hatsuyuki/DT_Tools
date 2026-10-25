using System.Collections.Generic;
using System.Globalization;
using DT_Tools.Game;

namespace DT_Tools.Commands
{
    internal sealed class AudioSourceArgs
    {
        public AudioPlaybackSource SourceKind { get; private set; } = AudioPlaybackSource.Auto;

        public float MaxSeconds { get; private set; } = -1f;

        public float Volume { get; private set; }

        public string Source { get; private set; }

        public static bool TryParse(string[] tokens, float defaultVolume, out AudioSourceArgs args, out string error)
        {
            args = new AudioSourceArgs { Volume = defaultVolume };
            error = null;
            if (tokens == null || tokens.Length == 0)
            {
                error = "缺少音频来源";
                return false;
            }

            float? seconds = null;
            float? volume = null;
            var rest = new List<string>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
            {
                var word = tokens[i].ToLowerInvariant();
                if (word == "local" || word == "本地")
                {
                    args.SourceKind = AudioPlaybackSource.Local;
                    continue;
                }
                if (word == "online" || word == "在线")
                {
                    args.SourceKind = AudioPlaybackSource.Online;
                    continue;
                }
                float value;
                if (i + 1 < tokens.Length
                    && float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    switch (word)
                    {
                        case "volume":
                        case "vol":
                        case "响度":
                        case "音量":
                            volume = value;
                            i++;
                            continue;
                        case "seconds":
                        case "sec":
                        case "秒":
                        case "时长":
                            seconds = value;
                            i++;
                            continue;
                    }
                }
                rest.Add(tokens[i]);
            }

            var positional = new List<float>(2);
            int end = rest.Count;
            int freeSlots = (seconds == null ? 1 : 0) + (volume == null ? 1 : 0);
            while (positional.Count < freeSlots && end > 1
                   && float.TryParse(rest[end - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float num))
            {
                positional.Insert(0, num);
                end--;
            }
            foreach (float num in positional)
            {
                if (seconds == null)
                    seconds = num;
                else
                    volume = num;
            }

            args.MaxSeconds = seconds ?? -1f;
            if (args.MaxSeconds <= 0f)
                args.MaxSeconds = -1f;
            args.Volume = volume ?? defaultVolume;
            if (args.Volume < 0f || args.Volume > 1f)
            {
                error = $"响度需在 0~1 之间: {args.Volume}";
                return false;
            }

            args.Source = string.Join(" ", rest.GetRange(0, end)).Trim();
            if (args.Source.Length == 0)
            {
                error = "缺少音频来源";
                return false;
            }
            return true;
        }
    }
}
