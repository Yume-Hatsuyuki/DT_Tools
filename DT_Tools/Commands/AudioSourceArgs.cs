using System.Collections.Generic;
using System.Globalization;
using DT_Tools.Game;

namespace DT_Tools.Commands
{
    /// <summary>
    /// 音频来源参数解析核心（/mic_music 与 /play_audio 共用）：
    /// &lt;url|路径&gt; + 任意顺序的参数（volume/秒 关键字、local/online 来源标记）。
    /// 引号分词在更上游的 CommandTokenizer 完成（带空格路径可整体加引号），本层把
    /// 路径 token 用空格拼回——引号非强制：直接连写同样成立，只有与数字参数产生
    /// 歧义时（如无扩展名文件名以数字结尾）才需要引号。
    /// 参数不约束顺序（像常规软件的命名参数）：local|online 标记与 volume/seconds
    /// 关键字出现在任何位置都生效；旧式位置写法（尾部最多两个纯数字，先 seconds
    /// 后 volume）仍兼容。关键字优先于位置参数，已被关键字占用的槽位不再从尾部吃数字。
    /// </summary>
    internal sealed class AudioSourceArgs
    {
        public AudioPlaybackSource SourceKind { get; private set; } = AudioPlaybackSource.Auto;

        /// <summary>时长上限（秒）。-1 = 不限（完整播放）。</summary>
        public float MaxSeconds { get; private set; } = -1f;

        /// <summary>响度（0~1，默认由命令自定：/mic_music 0.85、/play_audio 1）。</summary>
        public float Volume { get; private set; }

        /// <summary>url 或本地路径（路径 token 已用空格拼回、首尾引号已由分词器剥除）。</summary>
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

            // ① 来源标记与命名关键字：<标记/关键字> 成对或单个吃掉，出现位置任意。
            //    local/online（中文 本地/在线）可重复，后者覆盖前者；关键字后不是纯数字时
            //    整对保留为路径的一部分——文件/目录名里含 volume、秒 等词不受影响
            //    （"volume 2" 目录里的 token 是 "2\xxx"，非纯数字）。
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

            // ② 位置参数：剩余尾部最多吃「未指定槽位数」个纯数字（先 seconds 后 volume）；
            //    槽位全被关键字占用时不吃——数字属于路径（文件名以数字结尾的情形）。
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
                args.MaxSeconds = -1f;   // 任意非正值一律按「不限」处理（与阶段配置约定一致）
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
