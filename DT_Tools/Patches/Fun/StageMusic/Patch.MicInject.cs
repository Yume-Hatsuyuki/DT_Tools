using System;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using NAudio.Wave;
namespace DT_Tools.Patches.Fun.StageMusic
{
    /// <summary>
    /// 麦克风广播注入（实验性）——**不参与引擎自动挂载**（无 [HarmonyPatch] 特性）：
    /// 目标方法在运行时解析（EncoderPipeline 为 DissonanceVoip 内部管线，签名无法编译期
    /// 验证），若参与自动挂载，解析/绑定失败会触发功能级失败隔离，把整个 StageMusic
    /// 连同触发器一起跳过（历史事故：所有阶段音乐静默失效）。改由 Feature.OnEnabled
    /// 动态挂载并 try/catch，目标不可达时降级为仅本地播放。
    /// 原理：EncoderPipeline.ReceiveMicrophoneData（Dissonance.Audio.Capture，语音编码前
    /// 接收麦克风帧）前缀把阶段音乐 PCM 原地混入缓冲（共享数组，编码器随后读到混音结果），
    /// VAD/推送说话逻辑照常作用。麦克风静音时不混入（不强制开启麦克风）。
    /// NAudio（WaveFormat）内嵌于 DissonanceVoip.dll（已引用）。
    /// </summary>
    internal static class StageMusicMicInject
    {
        // 自持 Harmony 实例：不放在 StageMusicFeature 上（会被 FeatureLoader.HasSelfManagedHarmony
        // 误判为"整个 StageMusic 自管挂载"，导致 Patch.Triggers.cs 全部触发补丁被跳过，见 Feature.cs 注释）。
        private static readonly Harmony _harmony = new Harmony("DT_Tools.StageMusic.Mic");

        private static bool _tried;
        private static bool _mounted;

        /// <summary>Feature.OnEnabled 调用：动态解析并挂载（幂等，失败降级不抛出）。</summary>
        public static void TryMount()
        {
            if (_tried)
                return;
            _tried = true;
            try
            {
                var pipeline = AccessTools.TypeByName("Dissonance.Audio.Capture.EncoderPipeline");
                var target = pipeline == null ? null : AccessTools.Method(pipeline, "ReceiveMicrophoneData");
                if (target == null)
                {
                    Log.Warn<StageMusicFeature>("麦克风广播不可用：未找到 EncoderPipeline.ReceiveMicrophoneData，降级为仅本地播放");
                    return;
                }
                var prefix = new HarmonyMethod(typeof(StageMusicMicInject)
                    .GetMethod(nameof(MicPrefix), BindingFlags.NonPublic | BindingFlags.Static));
                _harmony.Patch(target, prefix: prefix);
                _mounted = true;
                Log.Info<StageMusicFeature>("麦克风广播注入已挂载");
            }
            catch (Exception ex)
            {
                Log.Warn<StageMusicFeature>("麦克风广播补丁挂载失败，降级为仅本地播放: " + ex.Message);
            }
        }

        public static bool Mounted => _mounted;

        private static void MicPrefix(ArraySegment<float> __0, WaveFormat __1)
        {
            if (__1 != null)
                StageMusicPlayer.MixIntoMic(__0, __1.SampleRate);
        }
    }
}
