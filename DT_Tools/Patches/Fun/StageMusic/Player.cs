using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DT_Tools.Core;
using Protocol;
using UnityEngine;
using UnityEngine.Networking;

namespace DT_Tools.Patches.Fun.StageMusic
{
    /// <summary>
    /// 播放器（单播放槽）：同 URI 播放中不重播（连杀防重复）、新触发停旧播新
    /// （切阶段只保留一首）、限长到点停止（-1/0 不限）。音频按 URI 缓存，
    /// 配置指纹变化时全部销毁重建（资源释放）。麦克风广播：播放时提取 PCM，
    /// 由 MicInjectPatch 在语音编码前混入（见 Patch.MicInject）。
    /// </summary>
    internal static class StageMusicPlayer
    {
        private static AudioSource _source;
        private static Coroutine _stopCo;
        private static string _playingUri;
        private static bool _manualHold;   // 手动点播独占播放槽：期间忽略阶段触发，结束/限长后恢复阶段音乐

        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private static string _configFingerprint = "\0";
        private static bool _loading;

        // ── 麦克风混音状态（MicInjectPatch 读取）──
        internal static float[] MixSamples;
        internal static int MixSampleRate;
        internal static float MixStartRealtime;
        internal static bool MixActive;

        /// <summary>阶段曲目配置（空 = 该阶段不播放）。</summary>
        private static string TrackOf(MusicStage stage)
        {
            switch (stage)
            {
                case MusicStage.Kill: return StageMusicFeature.KillTrack;
                case MusicStage.GiveKnife: return StageMusicFeature.GiveKnifeTrack;
                case MusicStage.Lobby: return StageMusicFeature.LobbyTrack;
                case MusicStage.Survive: return StageMusicFeature.SurviveTrack;
                case MusicStage.Detective: return StageMusicFeature.DetectTrack;
                case MusicStage.Trial: return StageMusicFeature.TrialTrack;
                case MusicStage.Execution: return StageMusicFeature.ExecutionTrack;
                case MusicStage.Victory: return StageMusicFeature.VictoryTrack;
                default: return "";
            }
        }

        public static void Play(MusicStage stage)
        {
            if (_manualHold)
                return;   // 手动点播独占播放槽（受 MaxSeconds 限长），期间忽略阶段触发

            string raw = TrackOf(stage)?.Trim() ?? "";
            if (raw.Length == 0)
            {
                StopCurrent();   // 切到未配置阶段：只保留一首的原则下停掉旧曲
                return;
            }
            PlayManual(raw);
        }

        /// <summary>
        /// 手动点播（/play_audio 与阶段触发共用）：同 URI 播放中不重播；
        /// 点播期间独占播放槽（阶段触发被忽略），结束/限长后自动恢复当前阶段音乐；
        /// 麦克风广播遵循 MicBroadcast 配置与静音状态（注入点与游戏阶段无关，全阶段可混入）。
        /// </summary>
        public static void PlayManual(string raw)
        {
            string uri = ResolveUri(raw);
            if (uri == null)
            {
                StopCurrent();
                return;
            }

            _manualHold = true;   // 独占：先置位，阶段触发在点播期间被忽略

            if (_playingUri == uri && _source != null && _source.isPlaying)
                return;          // 同曲目播放中：连杀/重复触发不重播

            EnsureConfigCache();
            if (_clips.TryGetValue(uri, out var cached) && cached != null)
            {
                StartPlayback(uri, cached);
                return;
            }
            if (_loading)
                return;
            CoroutineHost.Start(CoLoadAndPlay(uri));
        }

        /// <summary>停止当前播放（/stop_music 命令与阶段切换共用），并解除手动点播独占。</summary>
        public static void StopCurrent()
        {
            _playingUri = null;
            _manualHold = false;
            MixActive = false;
            if (_stopCo != null)
            {
                CoroutineHost.Stop(_stopCo);
                _stopCo = null;
            }
            if (_source != null && _source.isPlaying)
                _source.Stop();
        }

        private static IEnumerator CoLoadAndPlay(string uri)
        {
            if (_loading)
                yield break;
            _loading = true;
            var request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
            yield return request.SendWebRequest();
            _loading = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.Warn<StageMusicFeature>($"音频加载失败: {request.error} ({uri})");
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Log.Warn<StageMusicFeature>("音频解码失败（引擎不支持该格式）: " + uri);
                yield break;
            }
            _clips[uri] = clip;
            Log.Info<StageMusicFeature>($"音频就绪: {clip.length:F1} 秒 ({uri})");
            StartPlayback(uri, clip);
        }

        private static void StartPlayback(string uri, AudioClip clip)
        {
            var src = EnsureSource();
            if (src == null)
                return;

            if (_stopCo != null)
            {
                CoroutineHost.Stop(_stopCo);
                _stopCo = null;
            }
            _playingUri = uri;
            src.Stop();
            src.clip = clip;
            src.volume = Mathf.Clamp01(StageMusicFeature.Volume);
            src.Play();
            Log.Info<StageMusicFeature>($"阶段音乐播放: {uri}");

            PrepareMicMix(clip);

            float limit = StageMusicFeature.MaxSeconds;
            if (limit > 0f)
                _stopCo = CoroutineHost.Start(CoStopAfter(limit));
        }

        private static IEnumerator CoStopAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _stopCo = null;
            if (_source != null && _source.isPlaying)
                _source.Stop();

            if (_manualHold)
            {
                // 手动点播到限：解除独占并按当前阶段恢复阶段音乐（未配置阶段自动静默）
                _manualHold = false;
                ResumeStageMusic();
            }
        }

        /// <summary>按当前游戏状态恢复阶段音乐（阶段未配置曲目则保持静默）。</summary>
        private static void ResumeStageMusic()
        {
            var state = Managers.Game != null ? Managers.Game.State : EGameState.NoneState;
            Log.Info<StageMusicFeature>($"手动点播结束，恢复阶段音乐（{state}）");
            switch (state)
            {
                case EGameState.Lobby:
                    Play(MusicStage.Lobby);
                    break;
                case EGameState.Survive:
                    Play(MusicStage.Survive);
                    break;
                case EGameState.Detective:
                    Play(MusicStage.Detective);
                    break;
                case EGameState.Trial:
                    Play(MusicStage.Trial);
                    break;
                case EGameState.TotalResult:
                    Play(MusicStage.Victory);
                    break;
            }
        }

        // ── 配置缓存指纹 ──

        private static string ConfigFingerprint()
        {
            return StageMusicFeature.KillTrack + "\n" + StageMusicFeature.GiveKnifeTrack + "\n"
                + StageMusicFeature.LobbyTrack + "\n" + StageMusicFeature.SurviveTrack + "\n"
                + StageMusicFeature.DetectTrack + "\n" + StageMusicFeature.TrialTrack + "\n"
                + StageMusicFeature.ExecutionTrack + "\n" + StageMusicFeature.VictoryTrack;
        }

        /// <summary>配置变化时销毁全部缓存片段（资源释放），重建指纹。</summary>
        private static void EnsureConfigCache()
        {
            string fp = ConfigFingerprint();
            if (_configFingerprint == fp)
                return;
            foreach (var clip in _clips.Values)
            {
                if (clip != null)
                    UnityEngine.Object.Destroy(clip);
            }
            _clips.Clear();
            _configFingerprint = fp;
        }

        /// <summary>
        /// URI 解析：来源由 TrackSource 统一指定（不再按 http 前缀猜测，避免本地文件
        /// 命名含 http 字样时被误判）。Http 要求 http(s):// 前缀；Local 转为 file:/// URI。
        /// </summary>
        private static string ResolveUri(string raw)
        {
            if (StageMusicFeature.Source == TrackSource.Http)
            {
                if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warn<StageMusicFeature>("Http 模式需要 http(s):// 链接: " + raw);
                    return null;
                }
                return raw;
            }
            try
            {
                string full = Path.GetFullPath(raw);
                if (!File.Exists(full))
                {
                    Log.Warn<StageMusicFeature>("本地音频不存在: " + full);
                    return null;
                }
                return new Uri(full).AbsoluteUri;
            }
            catch (Exception ex)
            {
                Log.Warn<StageMusicFeature>("本地路径无效: " + ex.Message);
                return null;
            }
        }

        private static AudioSource EnsureSource()
        {
            if (_source != null)
                return _source;
            var go = new GameObject("DT_StageMusic");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            VoiceMixerHub.Route(_source, false);
            return _source;
        }

        // ── 麦克风混音（MicInjectPatch 调用）──

        /// <summary>播放时提取单声道 PCM 供语音编码前混入。</summary>
        private static void PrepareMicMix(AudioClip clip)
        {
            MixActive = false;
            if (!StageMusicFeature.MicBroadcast)
                return;
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
                MixSamples = mono;
                MixSampleRate = clip.frequency;
                MixStartRealtime = Time.realtimeSinceStartup;
                MixActive = true;
            }
            catch (Exception ex)
            {
                Log.Warn<StageMusicFeature>("麦克风混音准备失败: " + ex.Message);
                MixActive = false;
            }
        }

        /// <summary>
        /// 语音编码前把音乐混入麦克风帧（EncoderPipeline 收到的缓冲原地改写）。
        /// 麦克风静音时不混入（不强制开启麦克风）；音乐读完自动停止混入。
        /// </summary>
        public static void MixIntoMic(ArraySegment<float> buffer, int outputRate)
        {
            if (!MixActive || !StageMusicFeature.MicBroadcast)
                return;
            var comms = Managers.Voice?.Comms;
            if (comms != null && comms.IsMuted)
                return;    // 麦克风静音：仅本地收听，不广播

            float vol = Mathf.Clamp01(StageMusicFeature.Volume);
            double elapsed = Time.realtimeSinceStartup - MixStartRealtime;
            long head = (long)(elapsed * outputRate);
            for (int i = 0; i < buffer.Count; i++)
            {
                long pos = head + i;
                if (pos >= MixSamples.Length)
                {
                    MixActive = false;   // 音乐播完，停止混入
                    return;
                }
                int idx = (int)(pos * MixSampleRate / outputRate);
                if (idx >= MixSamples.Length)
                {
                    MixActive = false;
                    return;
                }
                int offset = buffer.Offset + i;
                float mixed = buffer.Array[offset] + MixSamples[idx] * vol;
                buffer.Array[offset] = Mathf.Clamp(mixed, -1f, 1f);
            }
        }
    }
}
