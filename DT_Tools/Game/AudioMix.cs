using System;
using System.IO;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Game
{
    /// <summary>
    /// 音频纯函数（Game 层共享）：StageMusic 播放器与点播引擎（AudioPlayback）是两台
    /// 同构引擎，播放槽状态（AudioSource/片段缓存/加载协程）各归各引擎，但无状态部分
    /// 只留一份实现——多声道下混提取、语音编码前混入、本地路径解析（0.1.16a 审计 F-M9/F-M18）。
    /// </summary>
    internal static class AudioMix
    {
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
        /// 把一段 PCM 混入语音编码前帧（缓冲原地改写，随每次语音帧调用）。
        /// 返回会话是否仍活跃：麦克风静音时原样返回 true（仅本地收听），
        /// PCM 读完返回 false，调用方据此关闭混入。
        /// </summary>
        internal static bool MixInto(float[] samples, int sampleRate, double startRealtime,
            float volume, bool active, ArraySegment<float> buffer, int outputRate)
        {
            if (!active || samples == null || samples.Length == 0)
                return false;
            var comms = Managers.Voice?.Comms;
            if (comms != null && comms.IsMuted)
                return true;    // 麦克风静音：不混入，会话保持
            double elapsed = Time.realtimeSinceStartup - startRealtime;
            long head = (long)(elapsed * outputRate);
            for (int i = 0; i < buffer.Count; i++)
            {
                long pos = head + i;
                if (pos >= samples.Length)
                    return false;   // 音乐播完，停止混入
                int idx = (int)(pos * sampleRate / outputRate);
                if (idx >= samples.Length)
                    return false;
                int offset = buffer.Offset + i;
                float mixed = buffer.Array[offset] + samples[idx] * volume;
                buffer.Array[offset] = Mathf.Clamp(mixed, -1f, 1f);
            }
            return true;
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
