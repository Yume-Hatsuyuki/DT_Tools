using System;
using System.IO;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Game
{
    internal static class AudioMix
    {
        internal static string ResolveSource(string raw, AudioPlaybackSource kind, string tag)
        {
            string trimmed = raw?.Trim() ?? "";
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
                default:
                    return isHttp ? trimmed : LocalFileUri(trimmed, tag);
            }
        }

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
            double cutoff = 0.5 * Math.Min(1.0, (double)dstRate / srcRate);
            double i0Beta = BesselI0(ResampleKaiserBeta);
            var table = new float[phases * taps];
            var w = new double[taps];
            for (int ph = 0; ph < phases; ph++)
            {
                double frac = (double)ph / phases;
                double sum = 0.0;
                for (int k = 0; k < taps; k++)
                {
                    int i = k - H + 1;
                    double d = frac - i;
                    double x = d / H;
                    double win = Math.Abs(x) >= 1.0 ? 0.0
                        : BesselI0(ResampleKaiserBeta * Math.Sqrt(1.0 - x * x)) / i0Beta;
                    double arg = 2.0 * Math.PI * cutoff * d;
                    double sinc = Math.Abs(arg) < 1e-12 ? 1.0 : Math.Sin(arg) / arg;
                    w[k] = 2.0 * cutoff * sinc * win;
                    sum += w[k];
                }
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

        internal sealed class ResampleJob
        {
            public volatile bool Done;
            public float[] Result;
            public int ResultRate;
            public Exception Error;
        }

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
                    job.Done = true;
                }
            });
            return job;
        }

        internal static double SecondsFromHead(int head, int outputRate)
        {
            return outputRate > 0 ? head / (double)outputRate : 0.0;
        }

        internal static int HeadFromSeconds(double seconds, int outputRate)
        {
            return (int)Math.Round(seconds * outputRate);
        }

        internal static int HeadEnd(int dataLength, int dataRate, int outputRate)
        {
            if (dataLength < 2 || dataRate <= 0 || outputRate <= 0)
                return 0;
            return (int)Math.Ceiling((double)(dataLength - 1) * outputRate / dataRate);
        }

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
                    head += i;
                    return false;
                }
                float frac = (float)(src - i0);
                float s = samples[i0] + (samples[i0 + 1] - samples[i0]) * frac;
                int offset = buffer.Offset + i;
                buffer.Array[offset] = SoftLimit(buffer.Array[offset] + s * volume);
            }
            head += count;
            return true;
        }

        private static float SoftLimit(float x)
        {
            float a = x < 0f ? -x : x;
            if (a <= 0.85f)
                return x;
            float y = 0.85f + 0.15f * (float)Math.Tanh((a - 0.85f) / 0.15f);
            return x < 0f ? -y : y;
        }

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
