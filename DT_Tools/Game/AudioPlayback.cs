using System.Collections;
using System.Collections.Generic;
using DT_Tools.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace DT_Tools.Game
{
    public enum AudioPlaybackSource
    {
        Auto = 0,
        Local = 1,
        Online = 2,
    }

    public static class AudioPlayback
    {
        private const string Tag = "AudioPlayback";

        private static AudioSource _source;
        private static Coroutine _stopCo;
        private static string _playingUri;
        private static long _loadGeneration;

        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private static readonly Queue<string> _clipOrder = new Queue<string>();
        private const int CacheCap = 32;
        private static bool _loading;
        private static float _pendingVolume = 1f;
        private static float _pendingMaxSeconds = -1f;
        private static long _pendingGeneration;

        public static bool Play(string rawSource, AudioPlaybackSource kind, float volume, float maxSeconds)
        {
            string uri = ResolveUri(rawSource, kind);
            if (uri == null)
                return false;
            PlayResolved(uri, Mathf.Clamp01(volume), maxSeconds);
            return true;
        }

        public static void Stop()
        {
            _playingUri = null;
            _loadGeneration++;
            if (_stopCo != null)
            {
                CoroutineHost.Stop(_stopCo);
                _stopCo = null;
            }
            if (_source != null && _source.isPlaying)
                _source.Stop();
        }

        private static string ResolveUri(string raw, AudioPlaybackSource kind)
            => AudioMix.ResolveSource(raw, kind, Tag);

        private static void PlayResolved(string uri, float volume, float maxSeconds)
        {
            if (_playingUri == uri && _source != null && _source.isPlaying)
                return;

            if (_clips.TryGetValue(uri, out var cached) && cached != null)
            {
                StartPlayback(uri, cached, volume, maxSeconds);
                return;
            }
            _pendingVolume = volume;
            _pendingMaxSeconds = maxSeconds;
            _pendingGeneration = ++_loadGeneration;
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
                yield break;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.Warn(Tag, $"音频加载失败: {request.error} ({uri})");
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Log.Warn(Tag, $"音频解码失败（引擎不支持该格式）: {uri}");
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
                    _clipOrder.Enqueue(oldest);
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
            VoiceMixerHub.Route(_source, false);
            return _source;
        }
    }
}
