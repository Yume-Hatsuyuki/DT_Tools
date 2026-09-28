using System.Globalization;

namespace DT_Tools.Commands.PlayAudio
{
    /// <summary>
    /// /play_audio 参数：&lt;url|路径&gt; [seconds] [volume]。
    /// 与「StageMusic」功能的任何 [Config] 字段无关——默认值在本类给出
    /// （完整播放 + 响度 1），不读取阶段配置。
    /// 从尾部起最多识别两个数字 token：先出现（更靠前）的是 seconds，其后是 volume；
    /// 只给一个数字时按 seconds 处理。source 取剩余 token 用空格拼回（url/路径本身不含空格）。
    /// </summary>
    internal sealed class PlayAudioArgs
    {
        public string Source { get; private set; }

        /// <summary>时长上限（秒）。-1 = 不限（默认：完整播放）。</summary>
        public float MaxSeconds { get; private set; } = -1f;

        /// <summary>音量（0~1，默认 1）。</summary>
        public float Volume { get; private set; } = 1f;

        public static bool TryParse(string[] tokens, out PlayAudioArgs args, out string error)
        {
            args = new PlayAudioArgs();
            error = null;

            if (tokens == null || tokens.Length == 0)
            {
                error = "缺少音频来源";
                return false;
            }

            int sourceEnd = tokens.Length;

            // 最多从尾部拿两个可解析成数字的 token：seconds 在前，volume 在后。
            // 一旦某个尾部 token 不是数字，立即停止（source 本身可能含数字，不能继续吃）。
            var trailing = new System.Collections.Generic.List<float>();
            while (trailing.Count < 2 && sourceEnd > 1
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

            // 任意非正值一律按「不限」处理（与阶段配置 -1/0=不限 的约定一致）。
            if (args.MaxSeconds <= 0f)
                args.MaxSeconds = -1f;

            if (args.Volume < 0f || args.Volume > 1f)
            {
                error = $"响度需在 0~1 之间: {args.Volume}";
                return false;
            }

            args.Source = string.Join(" ", tokens, 0, sourceEnd).Trim();
            if (args.Source.Length == 0)
            {
                error = "缺少音频来源";
                return false;
            }

            return true;
        }
    }
}
