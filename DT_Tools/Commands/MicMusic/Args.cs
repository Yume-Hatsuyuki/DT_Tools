using System.Globalization;
using System.Collections.Generic;
using DT_Tools.Game;

namespace DT_Tools.Commands.MicMusic
{
    /// <summary>
    /// /mic_music 参数：off 停止广播；否则为来源（local|online 前缀可选，缺省自动识别）
    /// + 尾部最多两个数字（seconds 在前、volume 在后）。解析模式与 PlayAudioArgs 一致。
    /// </summary>
    internal sealed class MicMusicArgs
    {
        public bool Off { get; private set; }
        public string Source { get; private set; }
        public AudioPlaybackSource SourceKind { get; private set; } = AudioPlaybackSource.Auto;
        public float MaxSeconds { get; private set; } = -1f;
        public float Volume { get; private set; } = 0.85f;   // 混入响度默认留出人声叠加余量（1.0 时低频常态进软限幅区产生谐波失真）

        public static bool TryParse(string[] tokens, out MicMusicArgs args, out string error)
        {
            args = new MicMusicArgs();
            if (tokens.Length == 0)
            {
                error = "缺少音频来源（或用 off 停止广播）";
                return false;
            }
            var first = tokens[0].ToLowerInvariant();
            if (first == "off" || first == "stop" || first == "停止")
            {
                args.Off = true;
                error = null;
                return true;
            }

            // 首个 token 为 local/online（或中文 本地/在线）时显式指定来源类型
            int start = 0;
            if (first == "local" || first == "本地")
            {
                args.SourceKind = AudioPlaybackSource.Local;
                start = 1;
            }
            else if (first == "online" || first == "在线")
            {
                args.SourceKind = AudioPlaybackSource.Online;
                start = 1;
            }

            // 最多从尾部拿两个可解析成数字的 token：seconds 在前，volume 在后。
            // 一旦某个尾部 token 不是数字，立即停止（source 本身可能含数字，不能继续吃）。
            var trailing = new List<float>();
            int sourceEnd = tokens.Length;
            while (trailing.Count < 2 && sourceEnd > start + 1
                   && float.TryParse(tokens[sourceEnd - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float num))
            {
                trailing.Insert(0, num);
                sourceEnd--;
            }
            if (trailing.Count == 1)
            {
                args.MaxSeconds = trailing[0];
            }
            else if (trailing.Count == 2)
            {
                args.MaxSeconds = trailing[0];
                args.Volume = trailing[1];
            }
            if (args.MaxSeconds <= 0f)
                args.MaxSeconds = -1f;   // 任意非正值一律按「不限」处理（与阶段配置约定一致）
            if (args.Volume < 0f || args.Volume > 1f)
            {
                error = $"响度需在 0~1 之间: {args.Volume}";
                return false;
            }

            args.Source = string.Join(" ", tokens, start, sourceEnd - start).Trim();
            if (string.IsNullOrEmpty(args.Source))
            {
                error = "缺少音频来源";
                return false;
            }
            error = null;
            return true;
        }
    }
}
