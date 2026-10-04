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
    /// （0.1.16b 审计 F-M9/F-M18）。
    /// </summary>
    internal static class AudioMix
    {
        /// <summary>
        /// 来源解析：Local 强制本地路径、Online 强制 http(s)、Auto 按前缀识别
        /// （http(s)=在线，其余本地）。解析失败告警并返回 null。
        /// </summary>
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
                        Log.Warn(tag, "local 指定了在线链接: " + trimmed);
                        return null;
                    }
                    return LocalFileUri(trimmed, tag);
                case AudioPlaybackSource.Online:
                    if (!isHttp)
                    {
                        Log.Warn(tag, "online 需要 http(s):// 链接: " + trimmed);
                        return null;
                    }
                    return trimmed;
                default:   // Auto
                    return isHttp ? trimmed : LocalFileUri(trimmed, tag);
            }
        }

        /// <summary>AudioClip → 单声道 PCM（多声道平均下混）。失败告警并返回 null。</summary>
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
                Log.Warn(tag, "麦克风混音准备失败: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 把一段 PCM 混入语音帧（缓冲原地改写，随每次麦克风帧调用）。
        /// head 为输出侧已混样本数（顺序推进——跟随麦克风采集时钟，不用墙钟对齐：
        /// 墙钟与采集时钟漂移、主线程卡顿丢帧都会造成混入位置跳变→可闻咔哒）。
        /// 取样用线性插值重采样（44.1k→48k 等）：整数截断会产生周期性走调/颤抖。
        /// 返回会话是否仍活跃：PCM 读完（含插值右邻）返回 false，调用方据此关闭混入。
        /// </summary>
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
                    return false;   // 音乐播完（不足一对插值样本），停止混入
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
        private static float SoftLimit(float x)
        {
            float a = x < 0f ? -x : x;
            if (a <= 0.85f)
                return x;
            float y = 0.85f + 0.15f * (float)Math.Tanh((a - 0.85f) / 0.15f);
            return x < 0f ? -y : y;
        }

        /// <summary>本地音频路径 → file:/// URI。路径无效告警并返回 null。</summary>
        internal static string LocalFileUri(string raw, string tag)
        {
            try
            {
                string full = Path.GetFullPath(raw);
                if (!File.Exists(full))
                {
                    Log.Warn(tag, "本地音频不存在: " + full);
                    return null;
                }
                return new Uri(full).AbsoluteUri;
            }
            catch (Exception ex)
            {
                Log.Warn(tag, "本地路径无效: " + ex.Message);
                return null;
            }
        }
    }
}
