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
    public static class MicBroadcast
    {
        private const string Tag = "MicBroadcast";

        private static readonly FieldInfo CaptureField =
            AccessTools.Field(typeof(DissonanceComms), "_capture");
        private static FieldInfo _microphoneField;

        private static AudioSource _source;
        private static Coroutine _loadCo;
        private static Coroutine _stopCo;
        private static long _loadGeneration;
        private static bool _loading;
        private static float _pendingVolume = 1f;
        private static float _pendingMaxSeconds = -1f;
        private static bool _localPlayback = true;

        private static AudioClip _clip;
        private static bool _micWanted;
        private static bool _micOptional;
        private static float[] _pendingMono;
        private static float _pendingStart;
        private static bool _localMuted;
        public static Action TrackFinished;

        private static MusicMicCapture _wrapper;

        internal static float[] MixSamples;
        internal static int MixSampleRate;
        internal static int MixHead;
        internal static float MixVolume;
        internal static bool MixActive;
        internal static int MixOutputRate;
        private static int _pendingRate;
        private static AudioMix.ResampleJob _mixJob;
        private static long _mixToken;
        private static bool _rateMismatchLogged;

        private static NoiseSuppressionLevels _savedDenoise;
        private static AecSuppressionLevels _savedAec;
        private static bool _savedRnnoise;
        private static float _savedCaptureGain;
        private static int _savedOpusBps;
        private static bool _profileTouched;

        private const int BroadcastOpusBps = 96000;

        private const int OpusAppVoip = 2048;
        private const int OpusAppAudio = 2049;
        private const int OpusCtlSetApplication = 4000;
        private const int OpusCtlGetApplication = 4001;

        [DllImport("opus", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dissonance_opus_encoder_ctl_in(IntPtr st, int request, int value);

        [DllImport("opus", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dissonance_opus_encoder_ctl_out(IntPtr st, int request, out int value);

        private static bool _opusAudioWanted;
        private static bool _handleProbeBroken;
        private static IntPtr _lastOpusHandle;
        private static bool _opusAudioApplied;
        private static FieldInfo _encPipelineField, _lockedEncField, _nativeEncField;

        public static bool Playing => _wrapper != null || _loading;

        public static bool Play(string rawSource, AudioPlaybackSource kind, float volume, float maxSeconds)
        {
            string uri = AudioMix.ResolveSource(rawSource, kind, Tag);
            if (string.IsNullOrEmpty(uri))
                return false;
            PlayResolved(uri, volume, maxSeconds, localPlayback: true);
            return true;
        }

        public static bool PlayTrack(string rawSource, float volume, bool micMix, float startSeconds = 0f, string userAgent = null)
        {
            string uri = AudioMix.ResolveSource(rawSource, AudioPlaybackSource.Auto, Tag);
            if (string.IsNullOrEmpty(uri))
                return false;
            Stop();
            _localPlayback = true;
            _micWanted = micMix;
            _micOptional = true;
            _pendingVolume = Mathf.Clamp01(volume);
            _pendingMaxSeconds = -1f;
            _pendingStart = Mathf.Max(0f, startSeconds);
            _loadGeneration++;
            _loading = true;
            _loadCo = CoroutineHost.Start(CoLoadAndBroadcast(uri, userAgent));
            return true;
        }

        public static void PlayResolved(string uri, float volume, float maxSeconds, bool localPlayback)
        {
            if (string.IsNullOrEmpty(uri))
                return;
            Stop();
            _localPlayback = localPlayback;
            _micWanted = true;
            _micOptional = false;
            _pendingVolume = Mathf.Clamp01(volume);
            _pendingMaxSeconds = maxSeconds;
            _loadGeneration++;
            _loading = true;
            _loadCo = CoroutineHost.Start(CoLoadAndBroadcast(uri, null));
        }

        public static void Stop()
        {
            _loadGeneration++;
            _loading = false;
            _clip = null;
            TrackFinished = null;
            _localMuted = false;
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
            _mixToken++;
            Unmount();
            RestoreBroadcastAudioProfile();
            if (_localPlayback && _source != null && _source.isPlaying)
                _source.Stop();
        }

        public static bool Seek(float seconds)
        {
            if (_clip == null || _loading)
                return false;
            float t = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, _clip.length - 0.05f));
            if (_source != null && _source.clip == _clip)
                _source.time = t;
            if (MixSamples != null && MixSampleRate > 0)
                MixHead = Mathf.Clamp(AudioMix.HeadFromSeconds(t, MixOutRate()), 0, MixHeadEnd());
            return true;
        }

        public static bool SetVolume(float volume)
        {
            float v = Mathf.Clamp01(volume);
            _pendingVolume = v;
            MixVolume = v;
            if (_source != null)
                _source.volume = v;
            return true;
        }

        public static bool SetLocalAudible(bool audible)
        {
            _localMuted = !audible;
            if (_source != null && _source.clip != null)
                _source.mute = !audible;
            return true;
        }

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
                    int micRate = PredictMicRate(_wrapper.Real);
                    MixOutputRate = micRate;
                    _mixToken++;
                    _mixJob = AudioMix.StartResample(mono, _clip.frequency, micRate);
                    CoroutineHost.Start(CoApplyResampledMix(_mixJob, mono, _clip.frequency, _mixToken));
                    MixVolume = _pendingVolume;
                }
                else
                {
                    MixOutputRate = PredictMicRate(_wrapper.Real);
                    SyncHeadToPlayback();
                    MixVolume = _pendingVolume;
                    MixActive = true;
                }
                Log.Info(Tag, "麦克风混入已开启（挂载虚拟麦克风并应用广播音频档案）");
                return true;
            }
            if (_wrapper == null)
                return true;
            Unmount();
            MixActive = false;
            Log.Info(Tag, "麦克风混入已关闭（采集管线还原，本地播放继续）");
            return true;
        }

        public sealed class TransportState
        {
            public string Phase;
            public float Position;
            public float Duration;
            public float Volume;
            public bool Mic;
            public bool Local;
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
                    position = (float)AudioMix.SecondsFromHead(MixHead, MixOutRate());
            }
            return new TransportState
            {
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

        private static IEnumerator CoLoadAndBroadcast(string uri, string userAgent)
        {
            long generation = _loadGeneration;
            var request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
            if (!string.IsNullOrEmpty(userAgent))
                request.SetRequestHeader("User-Agent", userAgent);
            yield return request.SendWebRequest();
            if (generation != _loadGeneration)
                yield break;
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
                Log.Warn(Tag, $"音频解码失败（引擎不支持该格式）: {uri}");
                yield break;
            }
            _clip = clip;

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
                        yield break;
                    if (_micOptional && pipeline == null)
                        break;
                    if (attempt == 0)
                        Log.Info(Tag, "麦克风采集暂不可用（可能正处于管线重启窗口），0.1s 间隔重试…");
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                if (real0 == null)
                {
                    if (!_micOptional)
                    {
                        Log.Warn(Tag, "广播失败：麦克风采集未运行（设备不可用或语音未连接），仅取消本次广播");
                        Stop();
                        yield break;
                    }
                    DowngradeToLocal("麦克风采集未运行（设备不可用或语音未连接）");
                }
                else if (!PrepareMix(clip, real0))
                {
                    if (!_micOptional)
                    {
                        Stop();
                        yield break;
                    }
                    DowngradeToLocal("混入源准备或麦克风挂载失败");
                }
                else
                {
                    yield return new WaitForSecondsRealtime(0.6f);
                    while (!_mixJob.Done)
                        yield return null;
                    TakeResampleResult();
                }
            }

            if (_localPlayback)
            {
                var src = EnsureSource();
                src.Stop();
                src.clip = clip;
                src.volume = _pendingVolume;
                src.mute = _localMuted;
                if (_pendingStart > 0f)
                    src.time = Mathf.Clamp(_pendingStart, 0f, Mathf.Max(0f, clip.length - 0.05f));
                src.Play();
            }

            if (_micWanted)
            {
                MixSamples = _pendingMono;
                _pendingMono = null;
                MixSampleRate = _pendingRate;
                MixHead = _pendingStart > 0f
                    ? Mathf.Clamp(AudioMix.HeadFromSeconds(_pendingStart, MixOutRate()), 0, MixHeadEnd())
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

        private static void TakeResampleResult()
        {
            var job = _mixJob;
            _mixJob = null;
            if (job == null)
                return;
            if (job.Error != null)
            {
                Log.Warn(Tag, job.Error, "混入源重采样失败，改用原采样率混入");
                return;
            }
            _pendingMono = job.Result;
            _pendingRate = job.ResultRate;
        }

        private static IEnumerator CoApplyResampledMix(AudioMix.ResampleJob job, float[] mono, int srcRate, long token)
        {
            while (!job.Done)
            {
                yield return null;
                if (token != _mixToken)
                    yield break;
            }
            if (token != _mixToken || _wrapper == null || _clip == null)
                yield break;
            if (job.Error != null)
            {
                Log.Warn(Tag, job.Error, "混入源重采样失败，改用原采样率混入");
                MixSamples = mono;
                MixSampleRate = srcRate;
            }
            else
            {
                MixSamples = job.Result;
                MixSampleRate = job.ResultRate;
            }
            SyncHeadToPlayback();
            MixActive = true;
        }

        private static void SyncHeadToPlayback()
        {
            if (_source != null && _source.clip == _clip)
                MixHead = Mathf.Clamp(AudioMix.HeadFromSeconds(_source.time, MixOutRate()), 0, MixHeadEnd());
        }

        private static int MixOutRate()
        {
            return MixOutputRate > 0 ? MixOutputRate : MixSampleRate;
        }

        private static int MixHeadEnd()
        {
            return MixSamples == null ? 0 : AudioMix.HeadEnd(MixSamples.Length, MixSampleRate, MixOutRate());
        }

        private static int PredictMicRate(IMicrophoneCapture real)
        {
            Microphone.GetDeviceCaps(real?.Device, out int min, out int max);
            return min == 0 && max == 0 ? 48000 : Mathf.Clamp(48000, min, max);
        }

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

        private static bool PrepareMix(AudioClip clip, IMicrophoneCapture real)
        {
            _pendingMono = AudioMix.ExtractMono(clip, Tag);
            if (_pendingMono == null)
                return false;
            _pendingRate = clip.frequency;
            _rateMismatchLogged = false;
            int micRate = PredictMicRate(real);
            MixOutputRate = micRate;
            _mixJob = AudioMix.StartResample(_pendingMono, clip.frequency, micRate);
            return TryMountMic(real);
        }

        private static void DowngradeToLocal(string reason)
        {
            _micWanted = false;
            _pendingMono = null;
            Log.Info(Tag, $"麦克风混入未启用（{reason}），本次改为纯本地播放");
        }

        private static IEnumerator CoTrackClock(float capSeconds)
        {
            float elapsed = 0f;
            while (true)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                if (capSeconds > 0f && elapsed >= capSeconds)
                    break;
                if (MixActive && MixSamples != null && MixHead >= MixHeadEnd())
                    break;
                if (_localPlayback && _source != null && _source.clip != null
                    && !_source.isPlaying && elapsed > 0.5f)
                    break;
            }
            _stopCo = null;
            bool natural = !(capSeconds > 0f && elapsed >= capSeconds);
            var finished = natural ? TrackFinished : null;
            Stop();
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
            VoiceMixerHub.Route(_source, false);
            return _source;
        }

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
                real.Subscribe(forward);
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
                Log.Warn(Tag, ex, "读取语音捕获管线失败");
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
                Log.Warn(Tag, ex, "读取麦克风采集器失败");
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
                Log.Error(Tag, ex, "写入麦克风采集器失败");
            }
        }

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
                Managers.Voice?.Comms?.ResetMicrophoneCapture();
                Log.Info(Tag, $"广播音频档案已应用：语音降噪/回声消除关停、Opus {BroadcastOpusBps}bps（原 {_savedOpusBps}bps），结束后自动还原");
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, ex, "应用广播音频档案失败（广播继续）");
            }
        }

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
                _opusAudioWanted = false;
                Managers.Voice?.Comms?.ResetMicrophoneCapture();
                Log.Info(Tag, "音频设置与 Opus 码率已还原");
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, ex, "还原音频设置失败");
            }
        }

        internal static void EnsureOpusAudioApplication()
        {
            if (!_opusAudioWanted)
                return;
            if (!TryGetNativeOpusHandle(out var handle))
                return;
            if (handle == _lastOpusHandle)
                return;
            _lastOpusHandle = handle;
            dissonance_opus_encoder_ctl_in(handle, OpusCtlSetApplication, OpusAppAudio);
            dissonance_opus_encoder_ctl_out(handle, OpusCtlGetApplication, out var readBack);
            _opusAudioApplied = readBack == OpusAppAudio;
            if (_opusAudioApplied)
                Log.Info(Tag, "Opus 编码模式已切至 Audio（高频不再被语音模式压制）");
            else
                Log.Warn(Tag, $"Opus 编码模式切换失败（读回 {readBack}），降级 96k VOIP 继续广播");
        }

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

        private static bool ProbeBroken(List<string> chain, string reason)
        {
            _handleProbeBroken = true;
            Log.Warn(Tag, $"Opus 原生句柄探测失败：{reason}（实机 DissonanceVoip 与反编译基准不一致）" +
                          $"——降级 96k VOIP 继续广播。反射链: {string.Join(" → ", chain)}");
            return false;
        }

        private sealed class MusicMicCapture : IMicrophoneCapture, IMicrophoneSubscriber, IMicrophoneDeviceList
        {
            internal readonly IMicrophoneCapture Real;
            private IMicrophoneSubscriber _forward;

            internal MusicMicCapture(IMicrophoneCapture real)
            {
                Real = real ?? throw new ArgumentNullException(nameof(real));
            }

            public bool IsRecording => Real.IsRecording;
            public string Device => Real.Device;
            public TimeSpan Latency => Real.Latency;
            public WaveFormat StartCapture(string name) => Real.StartCapture(name);
            public void StopCapture() => Real.StopCapture();
            public bool UpdateSubscribers() => Real.UpdateSubscribers();
            public void GetDevices(List<string> output) => (Real as IMicrophoneDeviceList)?.GetDevices(output);

            public void Subscribe(IMicrophoneSubscriber listener)
            {
                if (_forward != null)
                    Real.Unsubscribe(this);
                _forward = listener;
                Real.Subscribe(this);
            }

            public bool Unsubscribe(IMicrophoneSubscriber listener)
            {
                if (!ReferenceEquals(listener, _forward))
                    return Real.Unsubscribe(listener);
                _forward = null;
                return Real.Unsubscribe(this);
            }

            internal IMicrophoneSubscriber TakeForward()
            {
                var f = _forward;
                _forward = null;
                return f;
            }

            void IMicrophoneSubscriber.ReceiveMicrophoneData(ArraySegment<float> buffer, WaveFormat format)
            {
                if (MixActive)
                {
                    EnsureOpusAudioApplication();
                    MixOutputRate = format.SampleRate;
                    if (format.SampleRate != MixSampleRate && !_rateMismatchLogged)
                    {
                        _rateMismatchLogged = true;
                        Log.Warn(Tag, $"麦克风输出采样率 {format.SampleRate}Hz 与混入源 {MixSampleRate}Hz 不一致，改用线性插值混入（音质下降）");
                    }
                    MixActive = AudioMix.MixInto(MixSamples, MixSampleRate, MixVolume,
                        ref MixHead, buffer, format.SampleRate);
                }
                _forward?.ReceiveMicrophoneData(buffer, format);
            }

            void IMicrophoneSubscriber.Reset()
            {
                _forward?.Reset();
            }
        }
    }
}
