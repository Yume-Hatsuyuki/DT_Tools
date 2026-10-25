using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using UnityEngine;
using UnityEngine.Networking;

namespace DT_Tools.Features.StageMusic
{
    internal static class StageMusicPlayer
    {
        private static AudioSource _source;
        private static Coroutine _stopCo;
        private static string _playingUri;

        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private static readonly Queue<string> _clipOrder = new Queue<string>();
        private const int CacheCap = 32;
        private static string _configFingerprint = "\0";
        private static bool _loading;
        private static long _loadGeneration;
        private static long _pendingGeneration;
        private static float _pendingVolume = 1f;
        private static float _pendingMaxSeconds = -1f;

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
                case MusicStage.PickCharacter: return StageMusicFeature.PickCharacterTrack;
                case MusicStage.Discuss: return StageMusicFeature.DiscussTrack;
                case MusicStage.VotePhase: return StageMusicFeature.VotePhaseTrack;
                case MusicStage.VoteResult: return StageMusicFeature.VoteResultTrack;
                case MusicStage.Replay: return StageMusicFeature.ReplayTrack;
                case MusicStage.DiscoverCorpse: return StageMusicFeature.DiscoverCorpseTrack;
                case MusicStage.SelfDead: return StageMusicFeature.SelfDeadTrack;
                case MusicStage.EndingCutscene: return StageMusicFeature.EndingCutsceneTrack;
                case MusicStage.BlackSuccession: return StageMusicFeature.BlackSuccessionTrack;
                default: return "";
            }
        }

        private static float VolumeOf(MusicStage stage)
        {
            switch (stage)
            {
                case MusicStage.Kill: return StageMusicFeature.KillVolume;
                case MusicStage.GiveKnife: return StageMusicFeature.GiveKnifeVolume;
                case MusicStage.Lobby: return StageMusicFeature.LobbyVolume;
                case MusicStage.Survive: return StageMusicFeature.SurviveVolume;
                case MusicStage.Detective: return StageMusicFeature.DetectVolume;
                case MusicStage.Trial: return StageMusicFeature.TrialVolume;
                case MusicStage.Execution: return StageMusicFeature.ExecutionVolume;
                case MusicStage.Victory: return StageMusicFeature.VictoryVolume;
                case MusicStage.PickCharacter: return StageMusicFeature.PickCharacterVolume;
                case MusicStage.Discuss: return StageMusicFeature.DiscussVolume;
                case MusicStage.VotePhase: return StageMusicFeature.VotePhaseVolume;
                case MusicStage.VoteResult: return StageMusicFeature.VoteResultVolume;
                case MusicStage.Replay: return StageMusicFeature.ReplayVolume;
                case MusicStage.DiscoverCorpse: return StageMusicFeature.DiscoverCorpseVolume;
                case MusicStage.SelfDead: return StageMusicFeature.SelfDeadVolume;
                case MusicStage.EndingCutscene: return StageMusicFeature.EndingCutsceneVolume;
                case MusicStage.BlackSuccession: return StageMusicFeature.BlackSuccessionVolume;
                default: return 1f;
            }
        }

        private static float MaxSecondsOf(MusicStage stage)
        {
            switch (stage)
            {
                case MusicStage.Kill: return StageMusicFeature.KillMaxSeconds;
                case MusicStage.GiveKnife: return StageMusicFeature.GiveKnifeMaxSeconds;
                case MusicStage.Lobby: return StageMusicFeature.LobbyMaxSeconds;
                case MusicStage.Survive: return StageMusicFeature.SurviveMaxSeconds;
                case MusicStage.Detective: return StageMusicFeature.DetectMaxSeconds;
                case MusicStage.Trial: return StageMusicFeature.TrialMaxSeconds;
                case MusicStage.Execution: return StageMusicFeature.ExecutionMaxSeconds;
                case MusicStage.Victory: return StageMusicFeature.VictoryMaxSeconds;
                case MusicStage.PickCharacter: return StageMusicFeature.PickCharacterMaxSeconds;
                case MusicStage.Discuss: return StageMusicFeature.DiscussMaxSeconds;
                case MusicStage.VotePhase: return StageMusicFeature.VotePhaseMaxSeconds;
                case MusicStage.VoteResult: return StageMusicFeature.VoteResultMaxSeconds;
                case MusicStage.Replay: return StageMusicFeature.ReplayMaxSeconds;
                case MusicStage.DiscoverCorpse: return StageMusicFeature.DiscoverCorpseMaxSeconds;
                case MusicStage.SelfDead: return StageMusicFeature.SelfDeadMaxSeconds;
                case MusicStage.EndingCutscene: return StageMusicFeature.EndingCutsceneMaxSeconds;
                case MusicStage.BlackSuccession: return StageMusicFeature.BlackSuccessionMaxSeconds;
                default: return -1f;
            }
        }

        public static void Play(MusicStage stage)
        {
            string raw = TrackOf(stage)?.Trim() ?? "";
            if (raw.Length == 0)
            {
                StopCurrent();
                return;
            }
            string uri = ResolveUri(raw);
            if (uri == null)
                return;
            PlayInternal(uri, Mathf.Clamp01(VolumeOf(stage)), MaxSecondsOf(stage));
        }

        private static void PlayInternal(string uri, float volume, float maxSeconds)
        {
            if (_playingUri == uri && _source != null && _source.isPlaying)
                return;

            EnsureConfigCache();
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

        public static void StopCurrent()
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
            if (StageMusicFeature.MicBroadcast)
                MicBroadcast.Stop();
        }

        public static void Shutdown()
        {
            StopCurrent();
            if (_source != null)
            {
                UnityEngine.Object.Destroy(_source.gameObject);
                _source = null;
            }
            foreach (var clip in _clips.Values)
            {
                if (clip != null)
                    UnityEngine.Object.Destroy(clip);
            }
            _clips.Clear();
            _clipOrder.Clear();
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
                Log.Warn<StageMusicFeature>($"音频加载失败: {request.error} ({uri})");
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Log.Warn<StageMusicFeature>($"音频解码失败（引擎不支持该格式）: {uri}");
                yield break;
            }
            if (!_clips.ContainsKey(uri))
                _clipOrder.Enqueue(uri);
            _clips[uri] = clip;
            EvictCacheOverflow();
            Log.Info<StageMusicFeature>($"音频就绪: {clip.length:F1} 秒 ({uri})");
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
            Log.Info<StageMusicFeature>($"阶段音乐播放: {uri}");

            if (StageMusicFeature.MicBroadcast)
                MicBroadcast.PlayResolved(uri, volume, maxSeconds, localPlayback: false);

            if (maxSeconds > 0f)
                _stopCo = CoroutineHost.Start(CoStopAfter(maxSeconds));
        }

        private static IEnumerator CoStopAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _stopCo = null;
            if (_source != null && _source.isPlaying)
                _source.Stop();
        }

        private static string ConfigFingerprint()
        {
            var sb = new StringBuilder();
            foreach (MusicStage stage in Enum.GetValues(typeof(MusicStage)))
                sb.Append(TrackOf(stage)).Append('\n');
            return sb.ToString();
        }

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
            _clipOrder.Clear();
            _configFingerprint = fp;
        }

        private static string ResolveUri(string raw)
        {
            if (StageMusicFeature.Source == TrackSource.Http)
            {
                if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warn<StageMusicFeature>($"Http 模式需要 http(s):// 链接: {raw}");
                    return null;
                }
                return raw;
            }
            return AudioMix.LocalFileUri(raw, Engine.SectionOf<StageMusicFeature>());
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
    }
}
