using DT_Tools.Game;

namespace DT_Tools.Commands.MicMusic
{
    /// <summary>
    /// /mic_music 参数：off 停止广播；否则复用音频来源解析核心
    /// <see cref="AudioSourceArgs"/>（[local|online] &lt;url|路径&gt; [seconds] [volume]，
    /// 支持带空格路径加引号与 volume/seconds 命名关键字）。
    /// 混入响度默认 0.85 留出人声叠加余量（1.0 时低频常态进软限幅区产生谐波失真）。
    /// </summary>
    internal sealed class MicMusicArgs
    {
        /// <summary>混入响度默认值：留出人声叠加余量。</summary>
        internal const float DefaultVolume = 0.85f;

        public bool Off { get; private set; }
        public AudioSourceArgs Audio { get; private set; }

        // 透传：Command/Format 只关心来源解析结果（Off 路径下 Audio 为 null，调用方先行短路）
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
