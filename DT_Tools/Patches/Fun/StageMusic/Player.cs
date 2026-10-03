using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using UnityEngine;
using UnityEngine.Networking;

namespace DT_Tools.Patches.Fun.StageMusic
{
    /// <summary>
    /// 阶段音乐播放器（单播放槽）：同 URI 播放中不重播（连杀防重复）、新触发停旧播新
    /// （切阶段只保留一首）、限长到点停止（-1/0 不限）。音频按 URI 缓存，
    /// 配置指纹变化时全部销毁重建（资源释放）。本地播放；MicBroadcast 配置开启时
    /// 同时经 Game/MicBroadcast 虚拟麦克风混流发往全房（仅混流不本地重播，
    /// 广播随本播放槽启停；与 /mic_music 手动点歌互斥、后触发者接管）。
    /// 控制台点播已迁至 Game/AudioPlayback（独立引擎，互不共享状态）。
    /// </summary>
    internal static class StageMusicPlayer
    {
        private static AudioSource _source;
        private static Coroutine _stopCo;
        private static string _playingUri;

        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private static readonly Queue<string> _clipOrder = new Queue<string>();   // 插入序，供上限淘汰
        private const int CacheCap = 32;                 // 缓存上限：长会话换曲不至无限累积
        private static string _configFingerprint = "\0";
        private static bool _loading;
        private static long _loadGeneration;             // 加代替换计数：新触发使在途加载作废（对齐 AudioPlayback 语义）
        private static long _pendingGeneration;
        private static float _pendingVolume = 1f;      // 异步加载期间暂存本次播放的音量/时长（加载完成后交给 StartPlayback）
        private static float _pendingMaxSeconds = -1f;

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

        /// <summary>阶段独立音量（0~1）。</summary>
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

        /// <summary>阶段独立时长上限（秒）。-1/0 = 不限。</summary>
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

        /// <summary>按阶段播放：曲目/音量/时长上限均取该阶段独立配置，来源按 TrackSource 解析。</summary>
        public static void Play(MusicStage stage)
        {
            string raw = TrackOf(stage)?.Trim() ?? "";
            if (raw.Length == 0)
            {
                StopCurrent();   // 切到未配置阶段：只保留一首的原则下停掉旧曲
                return;
            }
            string uri = ResolveUri(raw);
            if (uri == null)
                return;   // 阶段解析失败：不打断已有播放（可能是另一首正常曲目）
            PlayInternal(uri, Mathf.Clamp01(VolumeOf(stage)), MaxSecondsOf(stage));
        }

        /// <summary>
        /// 播放核心（uri 已解析）：同 URI 播放中不重播，新触发停旧播新。
        /// </summary>
        private static void PlayInternal(string uri, float volume, float maxSeconds)
        {
            if (_playingUri == uri && _source != null && _source.isPlaying)
                return;          // 同曲目播放中：连杀/重复触发不重播

            EnsureConfigCache();
            if (_clips.TryGetValue(uri, out var cached) && cached != null)
            {
                StartPlayback(uri, cached, volume, maxSeconds);
                return;
            }
            _pendingVolume = volume;
            _pendingMaxSeconds = maxSeconds;
            _pendingGeneration = ++_loadGeneration;   // 加载中的新触发取代旧加载（旧语义静默丢弃已修）
            if (!_loading)
                CoroutineHost.Start(CoLoadAndPlay(uri));
        }

        /// <summary>停止当前阶段音乐（阶段切换触发与未配置阶段共用）。麦克风广播随播放槽一起停止。</summary>
        public static void StopCurrent()
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
            if (StageMusicFeature.MicBroadcast)
                MicBroadcast.Stop();
        }

        /// <summary>热关闭清理（Feature.OnDisabled 调用）：停播并销毁 AudioSource 与全部缓存片段。</summary>
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
                yield break;   // 加载期间来了新触发/停止：本次结果作废

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
            if (!_clips.ContainsKey(uri))
                _clipOrder.Enqueue(uri);
            _clips[uri] = clip;
            EvictCacheOverflow();
            Log.Info<StageMusicFeature>($"音频就绪: {clip.length:F1} 秒 ({uri})");
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
            Log.Info<StageMusicFeature>($"阶段音乐播放: {uri}");

            // 麦克风广播（总闸配置）：同一首曲子经虚拟麦克风混流发往全房。
            // 仅混流、本地仍由本播放器负责（不双源齐响）；时长跟随阶段配置。
            // 注：广播端有约 0.6s 编码器重建等待，对端起点略晚于本地，属预期。
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

        // ── 配置缓存指纹 ──

        private static string ConfigFingerprint()
        {
            // 全部阶段曲目参与指纹（Enum 驱动，新增阶段自动纳入）——指纹漏项会让
            // "配置变化销毁缓存"的承诺失效（审计 F-L65：2026-09 新增 9 阶段未纳入旧指纹）
            var sb = new StringBuilder();
            foreach (MusicStage stage in Enum.GetValues(typeof(MusicStage)))
                sb.Append(TrackOf(stage)).Append('\n');
            return sb.ToString();
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
            _clipOrder.Clear();
            _configFingerprint = fp;
        }

        /// <summary>
        /// URI 解析（阶段音乐用）：来源由 TrackSource 统一指定（不再按 http 前缀猜测，
        /// 避免本地文件命名含 http 字样时被误判）。Http 要求 http(s):// 前缀；
        /// Local 转为 file:/// URI。
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
