using System;
using System.Collections;
using System.Collections.Generic;
using DT_Tools.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace DT_Tools.Game
{
    /// <summary>点播来源类型。Auto=按前缀自动识别（http(s) 在线、其余本地）。</summary>
    public enum AudioPlaybackSource
    {
        Auto = 0,
        Local = 1,
        Online = 2,
    }

    /// <summary>
    /// 控制台点播引擎（/play_audio、/stop_music 专用，Game 层公共能力）：
    /// 与 StageMusic 补丁功能完全解耦——独立的播放槽状态，无状态部分共用 Game/AudioMix
    /// （见其头注释）。加载期间的新点播会取代旧加载（修复旧实现"加载中点播被静默丢弃"）。
    /// 麦克风广播：播放时提取单声道 PCM，由 StageMusic 的语音注入钩子混入编码前帧
    /// （总闸与注入点归属见 StageMusic/Patch.MicInject.cs）；
    /// 注入点缺席（「阶段音乐」未启用或挂载失败）时仅本地播放，并在播放路径告警一次。
    /// </summary>
    public static class AudioPlayback
    {
        private const string Tag = "AudioPlayback";

        private static AudioSource _source;
        private static Coroutine _stopCo;
        private static string _playingUri;
        private static float _currentVolume = 1f;
        private static long _loadGeneration;   // 加代替换计数：新点播使旧加载协程的结果作废

        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private static readonly Queue<string> _clipOrder = new Queue<string>();   // 插入序，供上限淘汰
        private const int CacheCap = 32;   // 缓存上限：长会话点播不至无限累积
        private static bool _loading;
        private static float _pendingVolume = 1f;
        private static float _pendingMaxSeconds = -1f;
        private static long _pendingGeneration;

        // ── 麦克风混音状态（StageMusic 语音注入钩子读取）──
        internal static float[] MixSamples;
        internal static int MixSampleRate;
        internal static float MixStartRealtime;
        internal static bool MixActive;

        /// <summary>
        /// 语音注入点是否已挂载（StageMusic 的 MicInject 挂载成功后置位）。
        /// Game 层不感知补丁层：只提供状态位，注入方负责写。
        /// </summary>
        internal static bool MicInjectMounted;
        private static bool _micInjectWarned;

        /// <summary>
        /// 点播入口：kind 指定来源类型（Local/Online 显式指定，Auto 按前缀识别），
        /// volume 0~1（调用方决定默认值），maxSeconds &lt;=0 表示完整播放。
        /// 解析失败只告警不打断当前播放（输错来源不应清空正在响的音乐）。
        /// </summary>
        public static void Play(string rawSource, AudioPlaybackSource kind, float volume, float maxSeconds)
        {
            string uri = ResolveUri(rawSource, kind);
            if (uri == null)
                return;
            PlayResolved(uri, Mathf.Clamp01(volume), maxSeconds);
        }

        /// <summary>停止当前点播（仅本引擎，不触碰阶段音乐）。</summary>
        public static void Stop()
        {
            _playingUri = null;
            MixActive = false;
            _loadGeneration++;   // 作废在途加载
            if (_stopCo != null)
            {
                CoroutineHost.Stop(_stopCo);
                _stopCo = null;
            }
            if (_source != null && _source.isPlaying)
                _source.Stop();
        }

        /// <summary>来源解析：Local 强制本地路径、Online 强制 http(s)、Auto 按前缀识别。</summary>
        private static string ResolveUri(string raw, AudioPlaybackSource kind)
        {
            string trimmed = raw?.Trim() ?? "";
            if (trimmed.Length == 0)
            {
                Log.Warn(Tag, "点播来源为空");
                return null;
            }
            bool isHttp = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            switch (kind)
            {
                case AudioPlaybackSource.Local:
                    if (isHttp)
                    {
                        Log.Warn(Tag, "local 指定了在线链接: " + trimmed);
                        return null;
                    }
                    return AudioMix.LocalFileUri(trimmed, Tag);
                case AudioPlaybackSource.Online:
                    if (!isHttp)
                    {
                        Log.Warn(Tag, "online 需要 http(s):// 链接: " + trimmed);
                        return null;
                    }
                    return trimmed;
                default:   // Auto
                    return isHttp ? trimmed : AudioMix.LocalFileUri(trimmed, Tag);
            }
        }

        private static void PlayResolved(string uri, float volume, float maxSeconds)
        {
            if (_playingUri == uri && _source != null && _source.isPlaying)
                return;          // 同曲目播放中：重复点播不重播

            if (_clips.TryGetValue(uri, out var cached) && cached != null)
            {
                StartPlayback(uri, cached, volume, maxSeconds);
                return;
            }
            _pendingVolume = volume;
            _pendingMaxSeconds = maxSeconds;
            _pendingGeneration = ++_loadGeneration;   // 新点播取代在途加载
            if (!_loading)
                CoroutineHost.Start(CoLoadAndPlay(uri));
        }

        private static IEnumerator CoLoadAndPlay(string uri)
        {
            _loading = true;
            long generation = _pendingGeneration;
            var request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
            yield return request.SendWebRequest();
            _loading = false;
            if (generation != _loadGeneration)
                yield break;   // 加载期间来了新点播/停止：本次结果作废

            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.Warn(Tag, $"音频加载失败: {request.error} ({uri})");
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Log.Warn(Tag, "音频解码失败（引擎不支持该格式）: " + uri);
                yield break;
            }
            _clips[uri] = clip;
            if (!_clipOrder.Contains(uri))
                _clipOrder.Enqueue(uri);
            EvictCacheOverflow();
            Log.Info(Tag, $"音频就绪: {clip.length:F1} 秒 ({uri})");
            if (generation != _loadGeneration)
                yield break;
            StartPlayback(uri, clip, _pendingVolume, _pendingMaxSeconds);
        }

        /// <summary>缓存上限淘汰：超过 CacheCap 时按插入序销毁最旧片段，正在播放的挪到队尾豁免。</summary>
        private static void EvictCacheOverflow()
        {
            while (_clipOrder.Count > CacheCap)
            {
                string oldest = _clipOrder.Peek();
                if (oldest == _playingUri && _clipOrder.Count == 1)
                    break;
                _clipOrder.Dequeue();
                if (oldest == _playingUri)
                {
                    _clipOrder.Enqueue(oldest);   // 播放中豁免，挪到队尾
                    continue;
                }
                if (_clips.TryGetValue(oldest, out var old) && old != null)
                    UnityEngine.Object.Destroy(old);
                _clips.Remove(oldest);
            }
        }

        private static void StartPlayback(string uri, AudioClip clip, float volume, float maxSeconds)
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
            _currentVolume = volume;
            src.Stop();
            src.clip = clip;
            src.volume = volume;
            src.Play();
            Log.Info(Tag, $"点播播放: {uri}（{(maxSeconds > 0f ? maxSeconds + " 秒" : "完整")}，响度 {volume:0.##}）");

            PrepareMicMix(clip);

            if (maxSeconds > 0f)
                _stopCo = CoroutineHost.Start(CoStopAfter(maxSeconds));
        }

        private static IEnumerator CoStopAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _stopCo = null;
            if (_source != null && _source.isPlaying)
                _source.Stop();
            if (_playingUri != null && _source != null && !_source.isPlaying)
                _playingUri = null;
            MixActive = false;
        }

        private static AudioSource EnsureSource()
        {
            if (_source != null)
                return _source;
            var go = new GameObject("DT_AudioPlayback");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            VoiceMixerHub.Route(_source, false);   // 0.1.16a VoiceMixerHub.cs:75，归入 Game 混音组
            return _source;
        }

        // ── 麦克风混音（StageMusic 的语音注入钩子调用）──

        /// <summary>播放时提取单声道 PCM 供语音编码前混入。</summary>
        private static void PrepareMicMix(AudioClip clip)
        {
            MixActive = false;
            var mono = AudioMix.ExtractMono(clip, Tag);
            if (mono == null)
                return;
            MixSamples = mono;
            MixSampleRate = clip.frequency;
            MixStartRealtime = Time.realtimeSinceStartup;
            MixActive = true;

            // 广播依赖语音注入钩子（由「阶段音乐」功能动态挂载）；缺席时点播只会本地响，
            // 这里显式告警避免「以为广播了其实没有」的静默失效（每次进程只提醒一次）。
            if (!MicInjectMounted && !_micInjectWarned)
            {
                _micInjectWarned = true;
                Log.Warn(Tag, "点播广播不可用：语音注入点未挂载（需启用「阶段音乐」功能），本次仅本地播放");
            }
        }

        /// <summary>
        /// 语音编码前把点播混入麦克风帧（缓冲原地改写）。是否调用由注入钩子决定
        /// （总闸 StageMusic.MicBroadcast）；麦克风静音时不混入；音乐读完自动停止。
        /// </summary>
        public static void MixIntoMic(ArraySegment<float> buffer, int outputRate)
        {
            MixActive = AudioMix.MixInto(MixSamples, MixSampleRate, MixStartRealtime,
                _currentVolume, MixActive, buffer, outputRate);
        }
    }
}
