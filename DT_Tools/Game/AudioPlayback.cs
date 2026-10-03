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
    /// 仅本地播放；对全房广播走独立命令 /mic_music（Game/MicBroadcast）。
    /// </summary>
    public static class AudioPlayback
    {
        private const string Tag = "AudioPlayback";

        private static AudioSource _source;
        private static Coroutine _stopCo;
        private static string _playingUri;
        private static long _loadGeneration;   // 加代替换计数：新点播使旧加载协程的结果作废

        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private static readonly Queue<string> _clipOrder = new Queue<string>();   // 插入序，供上限淘汰
        private const int CacheCap = 32;   // 缓存上限：长会话点播不至无限累积
        private static bool _loading;
        private static float _pendingVolume = 1f;
        private static float _pendingMaxSeconds = -1f;
        private static long _pendingGeneration;

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

        /// <summary>停止当前点播（仅本引擎，不触碰阶段音乐与虚拟麦克风）。</summary>
        public static void Stop()
        {
            _playingUri = null;
            _loadGeneration++;   // 作废在途加载
            if (_stopCo != null)
            {
                CoroutineHost.Stop(_stopCo);
                _stopCo = null;
            }
            if (_source != null && _source.isPlaying)
                _source.Stop();
        }

        /// <summary>来源解析：委托 AudioMix.ResolveSource（与 /mic_music 共用一份实现）。</summary>
        private static string ResolveUri(string raw, AudioPlaybackSource kind)
            => AudioMix.ResolveSource(raw, kind, Tag);

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
            src.Stop();
            src.clip = clip;
            src.volume = volume;
            src.Play();
            Log.Info(Tag, $"点播播放: {uri}（{(maxSeconds > 0f ? maxSeconds + " 秒" : "完整")}，响度 {volume:0.##}）");

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
        }

        private static AudioSource EnsureSource()
        {
            if (_source != null)
                return _source;
            var go = new GameObject("DT_AudioPlayback");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            VoiceMixerHub.Route(_source, false);   // 0.1.16b VoiceMixerHub.cs:75，归入 Game 混音组
            return _source;
        }
    }
}
