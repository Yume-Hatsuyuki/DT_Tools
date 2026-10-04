using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dissonance;
using Dissonance.Audio.Capture;
using Dissonance.Audio.Codecs.Opus;
using Dissonance.Config;
using DT_Tools.Core;
using HarmonyLib;
using NAudio.Wave;
using UnityEngine;
using UnityEngine.Networking;

namespace DT_Tools.Game
{
    /// <summary>
    /// 虚拟麦克风广播引擎（/mic_music、/stop_music 与随身MP3 应用共用，Game 层公共能力）：
    /// 运行期把 Dissonance 捕获管线上的采集器（CapturePipelineManager._microphone）临时替换为
    /// 包装器 MusicMicCapture——真实麦克风帧先到包装器、混入音乐 PCM、再转发 WebRTC 预处理管线，
    /// 之后照常 Opus 编码发送，全房间可闻。混流点在预处理与 VAD 上游，音乐自己触发语音激活，
    /// 无需说话（这是与旧 Harmony 注入方案的本质区别）。真实麦克风全程不被接替（仅叠加）；
    /// 播放结束或 /stop_music 时还原原采集器与订阅拓扑（「临时虚拟麦克风」语义）。
    /// 随身MP3 复用同一会话并叠加传输控制（跳转/响度/麦克风开关/本地静音/自然播完回调；
    /// 暂停在 API 层=销毁会话+书签，恢复=重建+起始偏移），
    /// micMix=false 时为纯本地播放（完全不碰语音管线）；三个入口后触发者接管（取代语义）。
    ///
    /// 挂载前提：游戏内麦克风未静音且非 PTT 抑制状态——Comms.IsMuted=true 时
    /// CapturePipelineManager.Update 强制退订编码器（DissonanceVoip-Decompile/
    /// CapturePipelineManager.cs:174），整条发送关闭，虚拟麦克风同样被门控；此处不绕过游戏自身语义。
    /// 音质（2026-10-03 实测修复）：混流帧会经过 WebRTC 预处理，其 RNNoise/WebRTC 降噪/AEC
    /// 均为语音专用、对音乐是灾难性破坏（谱门控压碎/NLP 镶边断续）——广播期间经 VoiceSettings
    /// （Dissonance public API，PropertyChanged 实时重配 native）临时全部关停、捕获增益归一，
    /// 结束自动还原；混入重采样用线性插值（44.1k→48k 整数截断会周期性走调）。残余的「变声器感」
    /// 是 Opus 32kbps VOIP 编码天花板，语音网络内无解。
    /// 时序健壮性：管线重启（帧跳/换设备/Pause→Resume/切麦克风）会重新 Subscribe 新 preprocessor
    /// （RestartTransmissionPipeline，cs:243），包装器幂等重绑覆盖；重进房间时 DissonanceComms
    /// 重建、旧包装器随旧管线消亡，卸载路径检测到字段已非自身包装器即跳过还原。
    /// </summary>
    public static class MicBroadcast
    {
        private const string Tag = "MicBroadcast";

        // 反射锚点（DissonanceVoip.dll，反编译基准 DissonanceVoip-Decompile/，2026-10-03 核对）：
        // Dissonance.DissonanceComms._capture : CapturePipelineManager（private，DissonanceComms.cs:50）
        // CapturePipelineManager 是 internal 类，编译期不可引用——管线对象与 _microphone 字段
        // 都从运行时类型解析（DissonanceComms.cs:29 _microphone : IMicrophoneCapture，private 非 readonly）
        private static readonly FieldInfo CaptureField =
            AccessTools.Field(typeof(DissonanceComms), "_capture");
        private static FieldInfo _microphoneField;

        private static AudioSource _source;
        private static Coroutine _loadCo;
        private static Coroutine _stopCo;
        private static long _loadGeneration;   // 加代替换计数：新点播/停止使在途加载作废
        private static bool _loading;
        private static float _pendingVolume = 1f;
        private static float _pendingMaxSeconds = -1f;
        private static bool _localPlayback = true;   // false = 仅混流不本地播（阶段音乐复用，本地归其播放器）

        // ── 传输控制（随身MP3 WebUI 应用驱动；/mic_music 路径不用，语义不变）──
        // 暂停不在引擎层：API 层「暂停=记书签+销毁会话、播放=重建+起始偏移」，
        // 引擎只有 播放中/加载中/空闲 三态，不存在挂起的僵尸会话。
        private static AudioClip _clip;        // 当前会话曲目（Seek/快照/完成判定需要，Stop 清空）
        private static bool _micWanted;        // 本会话是否要麦克风混入（false=纯本地，语音未连接也可播）
        private static float[] _pendingMono;   // 加载期提取的单声道 PCM，混入启动时交给 Mix 状态
        private static float _pendingStart;    // 起始偏移（秒）：恢复暂停书签/连播跳转用
        private static bool _localMuted;       // 本地喇叭静音偏好（mute 而非 Stop：保留播放时钟与混入头同步）
        /// <summary>自然播完回调（随身MP3 自动连播）；Stop() 清空，时长上限到点不触发。</summary>
        public static Action TrackFinished;

        // 当前挂载状态（_wrapper 为 null = 未挂载；包装器持有的真实采集器随其 Real 携带）
        private static MusicMicCapture _wrapper;

        // 混音状态（包装器每帧读取；head 顺序推进跟随麦克风采集时钟，见 AudioMix.MixInto）
        internal static float[] MixSamples;
        internal static int MixSampleRate;
        internal static int MixHead;
        internal static float MixVolume;
        internal static bool MixActive;

        // 广播期间被临时改写的音频设置（结束后还原；VoiceSettings 是 Dissonance public API，
        // 属性赋值经 PropertyChanged → WebRtcPreprocessor.Bind 实时重配 native，零反射）
        private static NoiseSuppressionLevels _savedDenoise;
        private static AecSuppressionLevels _savedAec;
        private static bool _savedRnnoise;
        private static float _savedCaptureGain;
        private static int _savedOpusBps;
        private static bool _profileTouched;

        // 广播期 Opus 码率：游戏固定 32000bps（VOIP 语音档）——音乐低频编码极差（"低音塑料"）。
        // OpusBitrateOverride.Bps 是 public static volatile，但只在编码器构造时读取
        // （OpusEncoder.cs GetTargetBitrate）——改完必须经 ResetMicrophoneCapture()（DissonanceComms
        // public，cs:724 → ForceReset）重建编码器才生效；结束还原后同样重建回 32k。
        // 残余限制：编码器 application 硬编码 VOIP（OpusNative.cs:165 传 2048，高音带宽偏窄），
        // 96k 下 Opus 自适应到混合模式已有实质改善；彻底解决需反射切 application，暂缓。
        private const int BroadcastOpusBps = 96000;

        // ── Opus 编码模式（Audio=2049，修"尖锐感"）──
        // AudioBench 离线实测（2026-10-03，用户 FLAC 全曲）：VOIP 模式 8-16kHz 能量只剩参考的
        // 12%（高频空洞 + SILK 伪影 = 尖锐感），Audio 模式恢复到 51%、SNR 14.5dB、相关 0.993；
        // 128k 相对 96k 无增益（饱和），重采样方式被编码吸收（线性插值即可，AudioMix 无需预重采样）。
        // 游戏 native 构造硬编码 application=2048（OpusNative.cs:165）且 Ctl 枚举未暴露
        // SetApplication——经 native CTL（OPUS_SET_APPLICATION=4000）直接改，反射穿透：
        // CapturePipelineManager._encoder（EncoderPipeline）→ ._encoder（ReadonlyLockedValue
        // <IVoiceEncoder>）→ Value = Dissonance OpusEncoder（internal）→ ._encoder（OpusNative.
        // OpusEncoder）→ ._encoder（LockedValue<IntPtr>）= native 句柄。编码器每次管线重启都会
        // 重建（构造回 VOIP），包装器每帧校验句柄、变了就重切（反射链每帧约 0.1% 开销）。
        private const int OpusAppVoip = 2048;
        private const int OpusAppAudio = 2049;
        private const int OpusCtlSetApplication = 4000;
        private const int OpusCtlGetApplication = 4001;

        [DllImport("opus", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dissonance_opus_encoder_ctl_in(IntPtr st, int request, int value);

        [DllImport("opus", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dissonance_opus_encoder_ctl_out(IntPtr st, int request, out int value);

        private static bool _opusAudioWanted;      // 广播期 = 需要 Audio 模式（随 profile 生命周期）
        private static bool _handleProbeBroken;    // 反射链结构性断裂（实机 DLL 字段缺失），一次性告警后永久降级
        private static IntPtr _lastOpusHandle;
        private static bool _opusAudioApplied;
        private static FieldInfo _encPipelineField, _lockedEncField, _nativeEncField;

        /// <summary>当前是否在虚拟麦克风广播（含音频加载中）。</summary>
        public static bool Playing => _wrapper != null || _loading;

        /// <summary>
        /// 广播入口：kind 指定来源类型（Local/Online 显式指定，Auto 按前缀识别），
        /// volume 0~1 同时控制本地响度与混入响度，maxSeconds &lt;=0 表示完整播放后卸载。
        /// 新点播取代进行中的广播（先还原再加载）。解析失败返回 false（原因已在
        /// ResolveSource 内告警），不改动进行中的广播。
        /// </summary>
        public static bool Play(string rawSource, AudioPlaybackSource kind, float volume, float maxSeconds)
        {
            string uri = AudioMix.ResolveSource(rawSource, kind, Tag);
            if (string.IsNullOrEmpty(uri))
                return false;
            PlayResolved(uri, volume, maxSeconds, localPlayback: true);
            return true;
        }

        /// <summary>
        /// 随身MP3 播放入口：完整播放（自然播完触发 <see cref="TrackFinished"/>），
        /// micMix=false 时不挂包装器、不应用广播档案（纯本地播放，语音未连接也可用），
        /// 混入随后可经 <see cref="SetMicMix"/> 随时开启。startSeconds=起始偏移（恢复暂停
        /// 书签时从原进度重建）。取代语义与 Play 一致。
        /// </summary>
        public static bool PlayTrack(string rawSource, float volume, bool micMix, float startSeconds = 0f)
        {
            string uri = AudioMix.ResolveSource(rawSource, AudioPlaybackSource.Auto, Tag);
            if (string.IsNullOrEmpty(uri))
                return false;
            Stop();
            _localPlayback = true;
            _micWanted = micMix;
            _pendingVolume = Mathf.Clamp01(volume);
            _pendingMaxSeconds = -1f;   // 不设上限：自然播完走 TrackFinished
            _pendingStart = Mathf.Max(0f, startSeconds);
            _loadGeneration++;
            _loading = true;
            _loadCo = CoroutineHost.Start(CoLoadAndBroadcast(uri));
            return true;
        }

        /// <summary>
        /// 广播入口（URI 已解析）：localPlayback=false 时只混流、不启动本地播放
        /// （阶段音乐复用——本地由 StageMusic 播放器负责，避免双 AudioSource 同曲齐响）。
        /// </summary>
        public static void PlayResolved(string uri, float volume, float maxSeconds, bool localPlayback)
        {
            if (string.IsNullOrEmpty(uri))
                return;
            Stop();   // 取代语义：先还原旧虚拟麦克风（含作废在途加载）
            _localPlayback = localPlayback;
            _micWanted = true;   // /mic_music 与阶段音乐两条复用路径都要混入
            _pendingVolume = Mathf.Clamp01(volume);
            _pendingMaxSeconds = maxSeconds;
            _loadGeneration++;
            _loading = true;
            _loadCo = CoroutineHost.Start(CoLoadAndBroadcast(uri));
        }

        /// <summary>停止广播：还原采集管线、停本地播放、作废在途加载。幂等。</summary>
        public static void Stop()
        {
            _loadGeneration++;
            _loading = false;
            _clip = null;
            TrackFinished = null;   // 连播回调随会话失效（随身MP3 每次 Play 重新挂）
            _localMuted = false;    // 静音偏好不跨会话：新广播（/mic_music）默认本地出声
            if (_loadCo != null)
            {
                CoroutineHost.Stop(_loadCo);
                _loadCo = null;
            }
            if (_stopCo != null)
            {
                CoroutineHost.Stop(_stopCo);
                _stopCo = null;
            }
            MixActive = false;
            MixSamples = null;
            Unmount();
            RestoreBroadcastAudioProfile();   // 兜底:档案应用后未及挂载即停止的路径（幂等）
            if (_localPlayback && _source != null && _source.isPlaying)
                _source.Stop();
        }

        // ── 传输控制（随身MP3 WebUI 应用驱动，全部主线程调用；/mic_music 不用）──
        // 暂停/恢复在 Mp3Api 层实现（暂停=记书签+Stop 销毁会话、恢复=PlayTrack 重建+起始偏移），
        // 引擎只有 播放中/加载中/空闲 三态，不存在挂起的僵尸会话。

        /// <summary>跳转（秒，自动夹紧到曲目范围）：本地播放位置与混入头一起拉到同一位置。</summary>
        public static bool Seek(float seconds)
        {
            if (_clip == null || _loading)
                return false;
            float t = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, _clip.length - 0.05f));
            if (_source != null && _source.clip == _clip)
                _source.time = t;   // 暂停中亦生效（恢复后从新位置继续）
            if (MixSamples != null && MixSampleRate > 0)
                MixHead = Mathf.Clamp((int)(t * MixSampleRate), 0, MixSamples.Length - 1);
            return true;
        }

        /// <summary>响度：会话中实时生效（本地源与混入一起），无会话时存为下次播放初值。</summary>
        public static bool SetVolume(float volume)
        {
            float v = Mathf.Clamp01(volume);
            _pendingVolume = v;
            MixVolume = v;
            if (_source != null)
                _source.volume = v;
            return true;
        }

        /// <summary>本地喇叭出声开关（mute 而非 Stop：保留播放时钟与混入头同步）。无会话时仅记录偏好，加载时应用。</summary>
        public static bool SetLocalAudible(bool audible)
        {
            _localMuted = !audible;
            if (_source != null && _source.clip != null)
                _source.mute = !audible;
            return true;
        }

        /// <summary>
        /// 麦克风混入开关（会话中切换）：开=挂包装器+应用广播档案（样本缺失时按需提取）；
        /// 关=卸载还原（语音管线与音频设置即时恢复原状），本地播放与会话不中断。
        /// 头位置对齐当前播放位置——混入关闭期间本地源照常推进，旧头位置已过期。
        /// </summary>
        public static bool SetMicMix(bool enabled)
        {
            if (_clip == null || _loading)
                return false;
            if (enabled)
            {
                if (_wrapper != null)
                    return true;
                if (!TryMountMic(null))
                    return false;
                if (MixSamples == null)
                {
                    var mono = AudioMix.ExtractMono(_clip, Tag);
                    if (mono == null)
                        return false;
                    MixSamples = mono;
                    MixSampleRate = _clip.frequency;
                }
                if (_source != null && _source.clip == _clip)
                    MixHead = Mathf.Clamp((int)(_source.time * MixSampleRate), 0, MixSamples.Length - 1);
                MixVolume = _pendingVolume;
                MixActive = true;
                Log.Info(Tag, "麦克风混入已开启（挂载虚拟麦克风并应用广播音频档案）");
                return true;
            }
            if (_wrapper == null)
                return true;
            Unmount();           // 还原管线与音频设置
            MixActive = false;   // 会话保留（本地继续播），完成判定回落到本地源
            Log.Info(Tag, "麦克风混入已关闭（采集管线还原，本地播放继续）");
            return true;
        }

        /// <summary>会话快照（随身MP3 状态轮询）：phase = idle | loading | playing | paused。</summary>
        public sealed class TransportState
        {
            public string Phase;
            public float Position;    // 秒（本地源时钟；纯混流时用混入头换算）
            public float Duration;    // 秒（无会话 = 0）
            public float Volume;      // 当前响度（下次播放初值）
            public bool Mic;          // 混入是否在送（包装器已挂载）
            public bool Local;        // 本地喇叭是否出声
        }

        public static TransportState Snapshot()
        {
            float duration = _clip != null ? _clip.length : 0f;
            float position = 0f;
            if (_clip != null && !_loading)
            {
                if (_source != null && _source.clip == _clip)
                    position = _source.time;
                else if (MixSamples != null && MixSampleRate > 0)
                    position = MixHead / (float)MixSampleRate;
            }
            return new TransportState
            {
                // 引擎只有三态；「已暂停」由 Mp3Api 以书签形式合并进状态（暂停=会话已销毁）
                Phase = _clip == null ? "idle"
                    : _loading ? "loading"
                    : "playing",
                Position = position,
                Duration = duration,
                Volume = _pendingVolume,
                Mic = _wrapper != null,
                Local = !_localMuted,
            };
        }

        private static IEnumerator CoLoadAndBroadcast(string uri)
        {
            long generation = _loadGeneration;
            var request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
            yield return request.SendWebRequest();
            if (generation != _loadGeneration)
                yield break;   // 加载期间来了新点播/停止：本次结果作废
            _loading = false;
            _loadCo = null;

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
            _clip = clip;

            // 麦克风混入前置校验放在加载完成后（避免加载期间语音系统状态变化）。
            // 上一会话的还原会触发采集管线重启（RestoreBroadcastAudioProfile →
            // ResetMicrophoneCapture），紧随其后的自动连播/恢复暂停会撞上重启窗口——
            // 此时采集器短暂「未在录」，立刻判死会让连播静默消失。给 ~2s 重试宽限。
            // 纯本地会话（_micWanted=false，随身MP3 关混入）不碰语音管线。
            // 此后任何失败路径都必须 Stop() 清场——否则 _clip 残留会让引擎永远报告
            // 「播放中」却无声（界面卡在暂停形态、只能手动停止的僵尸会话根源）。
            if (_micWanted)
            {
                IMicrophoneCapture real0 = null;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    var pipeline = ReadPipeline();
                    real0 = pipeline == null ? null : ReadMicrophone(pipeline);
                    if (real0 != null && real0.IsRecording)
                        break;
                    real0 = null;
                    if (generation != _loadGeneration)
                        yield break;   // 等待期间来了新点播/停止：本次结果作废
                    if (attempt == 0)
                        Log.Info(Tag, "麦克风采集暂不可用（可能正处于管线重启窗口），0.1s 间隔重试…");
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                if (real0 == null)
                {
                    Log.Warn(Tag, "广播失败：麦克风采集未运行（设备不可用或语音未连接），仅取消本次广播");
                    Stop();
                    yield break;
                }
                _pendingMono = AudioMix.ExtractMono(clip, Tag);
                if (_pendingMono == null)
                {
                    Stop();
                    yield break;
                }

                // 挂载包装器：顶替管线上的采集器（混流点在预处理/VAD 上游）。
                // 必须先挂载再应用音频档案——档案里的编码器重建（ForceReset）会走
                // RestartTransmissionPipeline，包装器已在字段上、自动重绑新 preprocessor。
                if (!TryMountMic(real0))
                {
                    Stop();
                    yield break;
                }

                // 等 ForceReset 生效：约 10 帧内部延迟 + 管线重启（期间麦克风短暂断流）。
                // 重建后的编码器才是广播码率——混入必须等它就绪，否则开头几秒仍走旧 32k 编码。
                yield return new WaitForSecondsRealtime(0.6f);
            }

            // 本地同步播放：有即时反馈；外放时真实麦克风拾音叠加，进一步稳住 VAD 触发。
            // 阶段音乐复用（localPlayback=false）时跳过——本地由 StageMusic 播放器负责。
            if (_localPlayback)
            {
                var src = EnsureSource();
                src.Stop();
                src.clip = clip;
                src.volume = _pendingVolume;
                src.mute = _localMuted;   // 随身MP3 本地静音偏好跨曲目保持
                if (_pendingStart > 0f)
                    src.time = Mathf.Clamp(_pendingStart, 0f, Mathf.Max(0f, clip.length - 0.05f));
                src.Play();
            }

            if (_micWanted)
            {
                MixSamples = _pendingMono;
                _pendingMono = null;
                MixSampleRate = clip.frequency;
                MixHead = _pendingStart > 0f
                    ? Mathf.Clamp((int)(_pendingStart * clip.frequency), 0, MixSamples.Length - 1)
                    : 0;
                MixVolume = _pendingVolume;
                MixActive = true;
            }
            _pendingStart = 0f;

            Log.Info(Tag, _micWanted
                ? (_localPlayback
                    ? $"虚拟麦克风广播: {uri}（响度 {_pendingVolume:0.##}，混入语音发往全房，播完自动卸载还原）"
                    : $"虚拟麦克风混流: {uri}（响度 {_pendingVolume:0.##}，仅混入发往全房、本地由调用方播放）")
                : $"随身播放器: {uri}（响度 {_pendingVolume:0.##}，纯本地播放、不混入麦克风）");
            _stopCo = CoroutineHost.Start(CoTrackClock(_pendingMaxSeconds));
        }

        /// <summary>
        /// 挂载虚拟麦克风（会话加载路径与随身MP3 SetMicMix(true) 共用）：校验采集在录 →
        /// 顶替 _microphone → 应用广播音频档案。失败已告警返回 false。
        /// </summary>
        private static bool TryMountMic(IMicrophoneCapture real)
        {
            var pipeline = ReadPipeline();
            real = real ?? (pipeline == null ? null : ReadMicrophone(pipeline));
            if (pipeline == null || real == null || !real.IsRecording)
            {
                Log.Warn(Tag, "麦克风混入开启失败：麦克风采集未运行（设备不可用或语音未连接）");
                return false;
            }
            _wrapper = new MusicMicCapture(real);
            WriteMicrophone(pipeline, _wrapper);
            ApplyBroadcastAudioProfile();
            return true;
        }

        /// <summary>
        /// 曲目时钟（逐帧）：自然播完（本地源播完 / 混入头走完 PCM）触发
        /// <see cref="TrackFinished"/>；时长上限到点（墙钟，/mic_music 语义）直接 Stop 不触发。
        /// 纯混流模式（StageMusic，localPlayback=false）以混入头/上限为完成依据。
        /// </summary>
        private static IEnumerator CoTrackClock(float capSeconds)
        {
            float elapsed = 0f;
            while (true)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                if (capSeconds > 0f && elapsed >= capSeconds)
                    break;   // 时长上限（墙钟）
                if (MixActive && MixSamples != null && MixHead >= MixSamples.Length - 1)
                    break;   // 混入头走完 PCM（麦克风帧停滞时头不动，不会误判播完）
                if (_localPlayback && _source != null && _source.clip != null
                    && !_source.isPlaying && elapsed > 0.5f)
                    break;   // 本地源播完（起步 0.5s 防加载竞态；纯本地模式唯一完成依据）
            }
            _stopCo = null;
            bool natural = !(capSeconds > 0f && elapsed >= capSeconds);
            var finished = natural ? TrackFinished : null;
            Stop();   // 播完即销毁：还原采集管线（含清 TrackFinished）
            finished?.Invoke();
        }

        private static AudioSource EnsureSource()
        {
            if (_source != null)
                return _source;
            var go = new GameObject("DT_MicBroadcast");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            VoiceMixerHub.Route(_source, false);   // 0.1.16b VoiceMixerHub.cs:75，归入 Game 混音组
            return _source;
        }

        // ── 挂载/卸载（全主线程：管线 Update、包装器回调、协程恢复点都在主线程，无需加锁）──

        /// <summary>卸载：还原订阅拓扑与 _microphone 字段。语音会话已重建（字段非本包装器）时跳过还原。</summary>
        private static void Unmount()
        {
            if (_wrapper == null)
                return;
            var wrapper = _wrapper;
            _wrapper = null;

            var pipeline = ReadPipeline();
            var current = pipeline == null ? null : ReadMicrophone(pipeline);
            if (pipeline == null || !ReferenceEquals(current, wrapper))
            {
                Log.Info(Tag, "语音会话已重建，旧虚拟麦克风随之失效（无需还原）");
                return;
            }

            var real = wrapper.Real;
            var forward = wrapper.TakeForward();
            real.Unsubscribe(wrapper);
            if (forward != null)
                real.Subscribe(forward);   // 恢复管线期望的直连拓扑（RestartTransmissionPipeline cs:243）
            WriteMicrophone(pipeline, real);
            RestoreBroadcastAudioProfile();
            Log.Info(Tag, "虚拟麦克风已卸载，采集管线与音频设置还原");
        }

        private static object ReadPipeline()
        {
            var comms = Managers.Voice?.Comms;
            if (comms == null)
                return null;
            try
            {
                return CaptureField?.GetValue(comms);
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, "读取语音捕获管线失败: " + ex.Message);
                return null;
            }
        }

        private static IMicrophoneCapture ReadMicrophone(object pipeline)
        {
            try
            {
                _microphoneField ??= AccessTools.Field(pipeline.GetType(), "_microphone");
                return _microphoneField?.GetValue(pipeline) as IMicrophoneCapture;
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, "读取麦克风采集器失败: " + ex.Message);
                return null;
            }
        }

        private static void WriteMicrophone(object pipeline, IMicrophoneCapture value)
        {
            try
            {
                _microphoneField.SetValue(pipeline, value);
            }
            catch (Exception ex)
            {
                Log.Error(Tag, "写入麦克风采集器失败: " + ex.Message);
            }
        }

        // ── 广播期间临时关停破坏性语音处理 ──
        // 实测（2026-10-03，用户）：游戏「噪音消除」(RNNoise) 与「回声消除」(AEC) 开启时音乐被
        // 谱门控/NLP 碾成碎片；此外游戏还有一层无 UI 开关的 WebRTC 降噪（VoiceManager 固定
        // DenoiseAmount=High）同样碾压音乐。三项在 DissonanceVoip-Decompile 均可配置：
        // VoiceSettings 属性 setter 触发 PropertyChanged → WebRtcPreprocessor.Bind 实时重配
        // native（WebRtcPreprocessingPipeline.cs:167-183），广播期临时关停、结束还原，用户无感。

        /// <summary>
        /// 记录并应用广播音频档案：关停 RNNoise / WebRTC 降噪 / AEC、捕获增益归一、Opus 码率提到 96k。
        /// 必须在挂载包装器之后调用（其中的编码器重建会经 RestartTransmissionPipeline 重绑包装器）。
        /// </summary>
        private static void ApplyBroadcastAudioProfile()
        {
            try
            {
                var vs = VoiceSettings.Instance;
                if (vs != null)
                {
                    _savedDenoise = vs.DenoiseAmount;
                    _savedAec = vs.AecSuppressionAmount;
                    _savedRnnoise = vs.BackgroundSoundRemovalEnabled;
                    vs.DenoiseAmount = NoiseSuppressionLevels.Disabled;
                    vs.AecSuppressionAmount = AecSuppressionLevels.Disabled;
                    vs.BackgroundSoundRemovalEnabled = false;
                }
                _savedCaptureGain = CaptureOutputGain.Value;
                if (_savedCaptureGain != 1f)
                    CaptureOutputGain.Value = 1f;
                _savedOpusBps = OpusBitrateOverride.Bps;
                OpusBitrateOverride.Bps = BroadcastOpusBps;
                _profileTouched = true;
                _opusAudioWanted = true;
                _opusAudioApplied = false;
                _lastOpusHandle = IntPtr.Zero;
                Managers.Voice?.Comms?.ResetMicrophoneCapture();   // 重建编码器：96k 下一帧生效（约 10 帧延迟+重启）
                Log.Info(Tag, $"广播音频档案已应用：语音降噪/回声消除关停、Opus {BroadcastOpusBps}bps（原 {_savedOpusBps}bps），结束后自动还原");
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, "应用广播音频档案失败（广播继续）: " + ex.Message);
            }
        }

        /// <summary>还原广播前的音频设置与 Opus 码率（码率还原后同样重建编码器）。幂等；异常打断也会经 Stop() 走到这里。</summary>
        private static void RestoreBroadcastAudioProfile()
        {
            if (!_profileTouched)
                return;
            _profileTouched = false;
            try
            {
                var vs = VoiceSettings.Instance;
                if (vs != null)
                {
                    vs.DenoiseAmount = _savedDenoise;
                    vs.AecSuppressionAmount = _savedAec;
                    vs.BackgroundSoundRemovalEnabled = _savedRnnoise;
                }
                CaptureOutputGain.Value = _savedCaptureGain;
                OpusBitrateOverride.Bps = _savedOpusBps;
                _opusAudioWanted = false;   // 编码器在下方 Reset 后重建，构造硬编码回 VOIP（天然还原）
                Managers.Voice?.Comms?.ResetMicrophoneCapture();   // 重建编码器回 32k（此时音乐已停，断音无感）
                Log.Info(Tag, "音频设置与 Opus 码率已还原");
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, "还原音频设置失败: " + ex.Message);
            }
        }

        // ── Opus 编码模式切换（Audio 模式，修"尖锐感"）──

        /// <summary>
        /// 广播期每帧校验：编码器句柄变了（管线重启重建，构造硬编码回 VOIP）或尚未切换时，
        /// 经 native CTL 把 application 切到 Audio。同一句柄只尝试一次，失败降级 96k VOIP（现状）。
        /// </summary>
        internal static void EnsureOpusAudioApplication()
        {
            if (!_opusAudioWanted)
                return;
            if (!TryGetNativeOpusHandle(out var handle))
                return;
            if (handle == _lastOpusHandle)
                return;   // 同一句柄只试一次（成功或失败都不再打扰）
            _lastOpusHandle = handle;
            dissonance_opus_encoder_ctl_in(handle, OpusCtlSetApplication, OpusAppAudio);
            dissonance_opus_encoder_ctl_out(handle, OpusCtlGetApplication, out var readBack);
            _opusAudioApplied = readBack == OpusAppAudio;
            if (_opusAudioApplied)
                Log.Info(Tag, "Opus 编码模式已切至 Audio（高频不再被语音模式压制）");
            else
                Log.Warn(Tag, $"Opus 编码模式切换失败（读回 {readBack}），降级 96k VOIP 继续广播");
        }

        /// <summary>
        /// 反射穿透四层私有字段取 native opus 编码器句柄（基准锚点：DissonanceVoip-Decompile，
        /// CapturePipelineManager.cs:33 / EncoderPipeline._encoder / OpusEncoder.cs:19 / OpusNative.cs:106）。
        /// 实机 DissonanceVoip 被厂商魔改、结构与基准有出入——每层记录实际类型名，探测失败时
        /// 整条链一次性输出（供按实机结构修正），随后永久降级 96k VOIP 防刷屏。
        /// </summary>
        private static bool TryGetNativeOpusHandle(out IntPtr handle)
        {
            handle = IntPtr.Zero;
            if (_handleProbeBroken)
                return false;
            var chain = new List<string>();
            try
            {
                var pipeline = ReadPipeline();
                if (pipeline == null)
                    return false;
                chain.Add(pipeline.GetType().FullName);
                _encPipelineField ??= AccessTools.Field(pipeline.GetType(), "_encoder");
                var encPipeline = _encPipelineField?.GetValue(pipeline);
                if (encPipeline == null)
                    return ProbeBroken(chain, "管线对象无 _encoder 字段或为空");
                chain.Add(encPipeline.GetType().FullName);
                _lockedEncField ??= AccessTools.Field(encPipeline.GetType(), "_encoder");
                var locked = _lockedEncField?.GetValue(encPipeline);
                if (locked == null)
                    return ProbeBroken(chain, "编码器管线无 _encoder 字段或为空");
                chain.Add(locked.GetType().FullName);
                var voiceEncField = AccessTools.Field(locked.GetType(), "_value");
                var voiceEncoder = voiceEncField?.GetValue(locked);
                if (voiceEncoder == null)
                    return ProbeBroken(chain, "锁定值无 _value 字段或为空");
                chain.Add(voiceEncoder.GetType().FullName);
                _nativeEncField ??= AccessTools.Field(voiceEncoder.GetType(), "_encoder");
                var native = _nativeEncField?.GetValue(voiceEncoder);
                if (native == null)
                    return ProbeBroken(chain, $"{voiceEncoder.GetType().Name} 无 _encoder 字段或为空");
                chain.Add(native.GetType().FullName);
                // native = OpusNative.OpusEncoder,其句柄在 _encoder(LockedValue<IntPtr>)里,再读一层 _value 才是指针
                var nativeLockedField = AccessTools.Field(native.GetType(), "_encoder");
                var nativeLocked = nativeLockedField?.GetValue(native);
                if (nativeLocked == null)
                    return ProbeBroken(chain, $"{native.GetType().Name} 无 _encoder 字段或为空");
                chain.Add(nativeLocked.GetType().FullName);
                var handleField = AccessTools.Field(nativeLocked.GetType(), "_value");
                if (handleField == null)
                    return ProbeBroken(chain, $"{nativeLocked.GetType().Name} 无 _value 字段");
                handle = (IntPtr)handleField.GetValue(nativeLocked)!;
                return handle != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, "获取 Opus 原生句柄失败: " + ex + " | 反射链: " + string.Join(" → ", chain));
                return false;
            }
        }

        /// <summary>反射链结构性断裂：一次性输出实机每层真实类型名，永久降级 96k VOIP 防刷屏。</summary>
        private static bool ProbeBroken(List<string> chain, string reason)
        {
            _handleProbeBroken = true;
            Log.Warn(Tag, $"Opus 原生句柄探测失败：{reason}（实机 DissonanceVoip 与反编译基准不一致）" +
                          $"——降级 96k VOIP 继续广播。反射链: {string.Join(" → ", chain)}");
            return false;
        }

        /// <summary>
        /// 虚拟麦克风包装器：对外是 IMicrophoneCapture（顶替管线上的采集器），对内以
        /// IMicrophoneSubscriber 订阅真实采集器——真实帧先到包装器、混入音乐、再转发原下游
        /// （WebRTC 预处理管线）。订阅侧"吞掉"管线对 preprocessor 的直连；管线重启会重新
        /// Subscribe 新 preprocessor，这里先摘旧再挂新（幂等）。同时实现 IMicrophoneDeviceList
        /// 防止任何 cast 使用点踩空（BasicMicrophoneCapture 实现了该接口）。全链路 Unity 主线程。
        /// </summary>
        private sealed class MusicMicCapture : IMicrophoneCapture, IMicrophoneSubscriber, IMicrophoneDeviceList
        {
            internal readonly IMicrophoneCapture Real;
            private IMicrophoneSubscriber _forward;   // 当前下游（通常是 WebRTC 预处理管线）；null=未接

            internal MusicMicCapture(IMicrophoneCapture real)
            {
                Real = real ?? throw new ArgumentNullException(nameof(real));
            }

            // ── 采集侧：纯透传（UpdateSubscribers 返回 true=请求管线重置，语义随真实采集器）──
            public bool IsRecording => Real.IsRecording;
            public string Device => Real.Device;
            public TimeSpan Latency => Real.Latency;
            public WaveFormat StartCapture(string name) => Real.StartCapture(name);
            public void StopCapture() => Real.StopCapture();
            public bool UpdateSubscribers() => Real.UpdateSubscribers();
            public void GetDevices(List<string> output) => (Real as IMicrophoneDeviceList)?.GetDevices(output);

            // ── 订阅侧：吞掉对下游的直连，把自己插到真实采集器与下游之间 ──
            public void Subscribe(IMicrophoneSubscriber listener)
            {
                if (_forward != null)
                    Real.Unsubscribe(this);   // 管线重启换新 preprocessor：先摘旧订阅防重复
                _forward = listener;
                Real.Subscribe(this);
            }

            public bool Unsubscribe(IMicrophoneSubscriber listener)
            {
                if (!ReferenceEquals(listener, _forward))
                    return Real.Unsubscribe(listener);   // 非下游订阅者（不该出现），原样透传
                _forward = null;
                return Real.Unsubscribe(this);           // 摘的是自己
            }

            /// <summary>卸载准备：摘下转发关系并返回原下游（还原时由引擎把下游直连回真实采集器）。</summary>
            internal IMicrophoneSubscriber TakeForward()
            {
                var f = _forward;
                _forward = null;
                return f;
            }

            // ── 数据侧：真实麦克风帧 → 混入音乐 → 转发下游（缓冲原地改写，编码器读到混音结果）──
            void IMicrophoneSubscriber.ReceiveMicrophoneData(ArraySegment<float> buffer, WaveFormat format)
            {
                if (MixActive)
                {
                    EnsureOpusAudioApplication();   // 编码器重建后自动重切 Audio 模式（帧跳/设备变化自愈）
                    MixActive = AudioMix.MixInto(MixSamples, MixSampleRate, MixVolume,
                        ref MixHead, buffer, format.SampleRate);
                }
                _forward?.ReceiveMicrophoneData(buffer, format);
            }

            void IMicrophoneSubscriber.Reset()
            {
                // 上游重置只影响实时麦克风流；音乐进度按墙钟（realtimeSinceStartup）对齐，无需回退
                _forward?.Reset();
            }
        }
    }
}
