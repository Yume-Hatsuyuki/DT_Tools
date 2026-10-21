using System;
using System.IO;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Game
{
    /// <summary>
    /// 音频纯函数（Game 层共享）：StageMusic 播放器、点播引擎（AudioPlayback）与虚拟麦克风
    /// 广播（MicBroadcast）是同构引擎，播放槽状态（AudioSource/片段缓存/加载协程）各归各引擎，
    /// 但无状态部分只留一份实现——多声道下混提取、语音帧混入、来源解析、本地路径解析
    /// （审计项 F-M9/F-M18）。
    /// </summary>
    internal static class AudioMix
    {
        /// <summary>
        /// 来源解析：Local 强制本地路径、Online 强制 http(s)、Auto 按前缀识别
        /// （http(s)=在线，其余本地）。
        /// </summary>
        /// <remarks>
        /// 解析失败告警并返回 null。
        /// </remarks>
        /// <param name="raw">原始来源文字。</param>
        /// <param name="kind">来源类型：本地、在线或自动识别。</param>
        /// <param name="tag">用于日志定位的标签。</param>
        /// <returns>解析后的音频地址。</returns>
        internal static string ResolveSource(string raw, AudioPlaybackSource kind, string tag)
        {
            string trimmed = raw?.Trim() ?? "";
            // 兜底剥首尾引号：命令行分词器（CommandTokenizer）已剥 token 引号，这里
            // 兜住经配置/API 等非分词通道直入的整串引号路径——"C:\a b.mp3" 原样进
            // Path.GetFullPath 会抛 Illegal characters。
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            if (trimmed.Length == 0)
            {
                Log.Warn(tag, "音频来源为空");
                return null;
            }
            bool isHttp = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            switch (kind)
            {
                case AudioPlaybackSource.Local:
                    if (isHttp)
                    {
                        Log.Warn(tag, $"local 指定了在线链接: {trimmed}");
                        return null;
                    }
                    return LocalFileUri(trimmed, tag);
                case AudioPlaybackSource.Online:
                    if (!isHttp)
                    {
                        Log.Warn(tag, $"online 需要 http(s):// 链接: {trimmed}");
                        return null;
                    }
                    return trimmed;
                default:   // Auto
                    return isHttp ? trimmed : LocalFileUri(trimmed, tag);
            }
        }

        /// <summary>
        /// AudioClip → 单声道 PCM（多声道平均下混）。
        /// </summary>
        /// <remarks>
        /// 失败告警并返回 null。
        /// </remarks>
        /// <param name="clip">要下混的音频片段。</param>
        /// <param name="tag">用于日志定位的标签。</param>
        /// <returns>单声道 PCM 样本；失败时为 null。</returns>
        internal static float[] ExtractMono(AudioClip clip, string tag)
        {
            try
            {
                int channels = clip.channels;
                var all = new float[clip.samples * channels];
                clip.GetData(all, 0);
                int frames = clip.samples;
                var mono = new float[frames];
                for (int i = 0; i < frames; i++)
                {
                    float sum = 0f;
                    for (int c = 0; c < channels; c++)
                        sum += all[i * channels + c];
                    mono[i] = sum / channels;
                }
                return mono;
            }
            catch (Exception ex)
            {
                Log.Warn(tag, ex, "麦克风混音准备失败");
                return null;
            }
        }

        /// <summary>
        /// 单声道高质量重采样（加载期一次性调用，结果交给 MixInto 按 1:1 取样）。
        /// </summary>
        /// <remarks>
        /// 线性插值没有抗混叠：44.1k→48k 时，输入 Nyquist 附近的内容会折叠成 16–24k 的镜像
        /// （音调测试：镜像仅比原音调低 10–18 dB）。这里用窗函数 sinc：Kaiser 窗 β=8.6（阻带约 −90 dB），
        /// 每侧 16 个输入采样，截止取两侧采样率中较低者的 Nyquist。相位表按 L=dstRate/gcd 预计算，
        /// 循环内只做乘加；不引用 Unity API，可在线程池执行。
        /// 相位数超过 4096 时（如 47999↔48000）返回 null，调用方改按原采样率混入。
        /// </remarks>
        /// <param name="src">源单声道 PCM。</param>
        /// <param name="srcRate">源采样率（Hz）。</param>
        /// <param name="dstRate">目标采样率（Hz）。</param>
        /// <returns>重采样后的 PCM；不支持时为 null；采样率相同时原样返回。</returns>
        internal static float[] ResampleMono(float[] src, int srcRate, int dstRate)
        {
            if (src == null || srcRate <= 0 || dstRate <= 0)
                return null;
            if (srcRate == dstRate)
                return src;
            int g = Gcd(srcRate, dstRate);
            int phases = dstRate / g;
            int step = srcRate / g;
            if (phases > ResamplePhaseLimit)
                return null;
            const int H = ResampleHalfTaps;
            const int taps = 2 * H;
            double cutoff = 0.5 * Math.Min(1.0, (double)dstRate / srcRate);   // 相对输入采样率的截止（周期/采样）
            double i0Beta = BesselI0(ResampleKaiserBeta);
            var table = new float[phases * taps];
            var w = new double[taps];
            for (int ph = 0; ph < phases; ph++)
            {
                double frac = (double)ph / phases;
                double sum = 0.0;
                for (int k = 0; k < taps; k++)
                {
                    int i = k - H + 1;                 // 输入采样偏移 -H+1 .. H
                    double d = frac - i;               // 输出时刻到该输入采样的距离（输入采样单位）
                    double x = d / H;
                    double win = Math.Abs(x) >= 1.0 ? 0.0
                        : BesselI0(ResampleKaiserBeta * Math.Sqrt(1.0 - x * x)) / i0Beta;
                    double arg = 2.0 * Math.PI * cutoff * d;
                    double sinc = Math.Abs(arg) < 1e-12 ? 1.0 : Math.Sin(arg) / arg;
                    w[k] = 2.0 * cutoff * sinc * win;
                    sum += w[k];
                }
                // 逐相位归一化：每个输出时刻的 DC 增益严格为 1，不依赖截止取值的近似。
                for (int k = 0; k < taps; k++)
                    table[ph * taps + k] = (float)(w[k] / sum);
            }
            long lastOut = (long)(src.Length - 1) * phases / step;
            var dst = new float[lastOut + 1];
            int last = src.Length - 1;
            for (long n = 0; n <= lastOut; n++)
            {
                long m = n * step;
                int baseIdx = (int)(m / phases);
                int tb = (int)(m % phases) * taps;
                int start = baseIdx - H + 1;
                double acc = 0.0;
                if (start >= 0 && start + taps - 1 <= last)
                {
                    for (int k = 0; k < taps; k++)
                        acc += src[start + k] * table[tb + k];
                }
                else
                {
                    for (int k = 0; k < taps; k++)
                    {
                        int idx = start + k;
                        if (idx >= 0 && idx <= last)
                            acc += src[idx] * table[tb + k];
                    }
                }
                dst[n] = (float)acc;
            }
            return dst;
        }

        private const int ResampleHalfTaps = 16;
        private const double ResampleKaiserBeta = 8.6;
        private const int ResamplePhaseLimit = 4096;

        private static int Gcd(int a, int b)
        {
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }
            return a;
        }

        /// <summary>
        /// 修正第一类零阶贝塞尔函数（Kaiser 窗用），幂级数展开，项数足够收敛。
        /// </summary>
        private static double BesselI0(double x)
        {
            double sum = 1.0, term = 1.0, q = x * x / 4.0;
            for (int k = 1; k < 200; k++)
            {
                term *= q / ((double)k * k);
                sum += term;
                if (term < 1e-14 * sum)
                    break;
            }
            return sum;
        }

        /// <summary>
        /// 后台重采样任务：线程池执行纯数学计算，主线程轮询 Done 后取结果（不用 async/await，netstandard2.1）。
        /// </summary>
        internal sealed class ResampleJob
        {
            public volatile bool Done;
            /// <summary>重采样结果；不支持时为原样本（与 ResultRate 配套）。</summary>
            public float[] Result;
            /// <summary>结果所在的采样率。</summary>
            public int ResultRate;
            public Exception Error;
        }

        /// <summary>
        /// 启动后台重采样。
        /// </summary>
        /// <param name="mono">源单声道 PCM（只读，任务期间不得修改）。</param>
        /// <param name="srcRate">源采样率（Hz）。</param>
        /// <param name="dstRate">目标采样率（Hz），通常为麦克风输出采样率。</param>
        /// <returns>可轮询的任务句柄。</returns>
        internal static ResampleJob StartResample(float[] mono, int srcRate, int dstRate)
        {
            var job = new ResampleJob();
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    float[] r = ResampleMono(mono, srcRate, dstRate);
                    job.Result = r ?? mono;
                    job.ResultRate = r == null ? srcRate : dstRate;
                }
                catch (Exception ex)
                {
                    job.Error = ex;
                }
                finally
                {
                    job.Done = true;   // volatile 写：Result/ResultRate/Error 先于 Done 对主线程可见
                }
            });
            return job;
        }

        /// <summary>
        /// 混入头（输出侧样本计数，见 MixInto）→ 曲目秒数。
        /// </summary>
        /// <remarks>
        /// 头按麦克风输出采样率计数，换算只依赖输出采样率，与混入源的采样率无关；
        /// 这样重采样前后、采样率不一致的回退路径都用同一套公式。
        /// </remarks>
        internal static double SecondsFromHead(int head, int outputRate)
        {
            return outputRate > 0 ? head / (double)outputRate : 0.0;
        }

        /// <summary>
        /// 曲目秒数 → 混入头（输出侧样本计数）。
        /// </summary>
        internal static int HeadFromSeconds(double seconds, int outputRate)
        {
            return (int)Math.Round(seconds * outputRate);
        }

        /// <summary>
        /// 混入头走完 PCM 的位置。
        /// </summary>
        /// <remarks>
        /// MixInto 在 floor(head × ratio) ≥ len−1 时停止（ratio = 数据采样率 / 输出采样率），
        /// 所以完成判定也要用同一个比例。数据与输出采样率相同时等于 len−1。
        /// </remarks>
        internal static int HeadEnd(int dataLength, int dataRate, int outputRate)
        {
            if (dataLength < 2 || dataRate <= 0 || outputRate <= 0)
                return 0;
            return (int)Math.Ceiling((double)(dataLength - 1) * outputRate / dataRate);
        }

        /// <summary>
        /// 把一段 PCM 混入语音帧（缓冲原地改写，随每次麦克风帧调用）。
        /// </summary>
        /// <remarks>
        /// head 为输出侧已混样本数（顺序推进——跟随麦克风采集时钟，不用墙钟对齐：
        /// 墙钟与采集时钟漂移、主线程卡顿丢帧都会造成混入位置跳变→可闻咔哒）。
        /// 取样用线性插值重采样（44.1k→48k 等）：整数截断会产生周期性走调/颤抖。
        /// 返回会话是否仍活跃：PCM 读完（含插值右邻）返回 false，调用方据此关闭混入。
        /// 秒与头之间的换算必须走 SecondsFromHead / HeadFromSeconds / HeadEnd（以输出采样率计数），
        /// 不得直接按曲目采样率换算：采样率不一致时两者相差 sampleRate/outputRate（44.1k 歌曲在 48k 麦克风上约 8%）。
        /// 加载期已把 PCM 重采样到麦克风采样率时 ratio 恒为 1，线性插值只在采样率不一致的回退路径上生效。
        /// </remarks>
        internal static bool MixInto(float[] samples, int sampleRate, float volume,
            ref int head, ArraySegment<float> buffer, int outputRate)
        {
            if (samples == null || samples.Length < 2 || volume <= 0f)
                return false;
            double ratio = (double)sampleRate / outputRate;
            int count = buffer.Count;
            for (int i = 0; i < count; i++)
            {
                double src = (head + i) * ratio;
                int i0 = (int)src;
                if (i0 >= samples.Length - 1)
                {
                    head += i;      // 本块中已混入的前 i 个样本仍需计入头，否则末尾少算最多一个块（约 10ms）
                    return false;   // 音乐播完（不足一对插值样本），停止混入
                }
                float frac = (float)(src - i0);
                float s = samples[i0] + (samples[i0 + 1] - samples[i0]) * frac;
                int offset = buffer.Offset + i;
                buffer.Array[offset] = SoftLimit(buffer.Array[offset] + s * volume);
            }
            head += count;
            return true;
        }

        /// <summary>
        /// 0.85 软拐点限幅：常态直通，峰值平滑压入 [-1,1)。音乐+人声叠加常态越限，
        /// 硬 Clamp 在峰值上产生可闻咔哒；拐点抬高到 0.85 减少大振幅低频（贝斯）常态
        /// 进入 tanh 区的谐波失真（"塑料感"），代价是溢出余量收窄。
        /// </summary>
        /// <param name="x">待限幅的采样值。</param>
        /// <returns>限幅后的采样值，范围 [-1, 1)。</returns>
        private static float SoftLimit(float x)
        {
            float a = x < 0f ? -x : x;
            if (a <= 0.85f)
                return x;
            float y = 0.85f + 0.15f * (float)Math.Tanh((a - 0.85f) / 0.15f);
            return x < 0f ? -y : y;
        }

        /// <summary>
        /// 本地音频路径 → file:/// URI。
        /// </summary>
        /// <remarks>
        /// 路径无效告警并返回 null。
        /// </remarks>
        /// <param name="raw">本地音频路径。</param>
        /// <param name="tag">用于日志定位的标签。</param>
        /// <returns>file:/// 形式的 URI；路径无效时为 null。</returns>
        internal static string LocalFileUri(string raw, string tag)
        {
            try
            {
                string full = Path.GetFullPath(raw);
                if (!File.Exists(full))
                {
                    Log.Warn(tag, $"本地音频不存在: {full}");
                    return null;
                }
                return new Uri(full).AbsoluteUri;
            }
            catch (Exception ex)
            {
                Log.Warn(tag, ex, "本地路径无效");
                return null;
            }
        }
    }
}
