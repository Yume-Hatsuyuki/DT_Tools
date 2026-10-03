using System;
using System.Collections;
using System.Text;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Patches.Experience.SilentWatcher
{
    /// <summary>
    /// 沉默猎人的核心逻辑（普通类，非 Harmony 补丁）。
    /// 订阅 OnChatReceived 记录最后发言时间与发言人；常驻协程周期扫描：
    /// 全场超过 TimeoutSeconds 无人发言时，广播调侃最后发言者（带已沉默秒数）。
    /// 协程随插件常驻（CoroutineHost 是 DontDestroyOnLoad 宿主），无销毁钩子，无需 Stop。
    /// </summary>
    internal static class SilentWatcherLogic
    {
        private static bool _subscribed;
        private static float _lastChatAt = -1f;
        private static string _lastSpeaker;
        private static float _lastTauntAt = -1f;

        public static void EnsureSubscribed(VoiceManager voice)
        {
            if (_subscribed)
                return;
            if (!Engine.Enabled<SilentWatcherFeature>())
                return;
            _subscribed = true;
            _lastChatAt = Time.unscaledTime;
            voice.OnChatReceived += OnChatReceived;
            CoroutineHost.Start(ScanLoop());
        }

        private static void OnChatReceived(VoiceManager.ChatPayload payload, bool isDead)
        {
            _lastChatAt = Time.unscaledTime;
            _lastSpeaker = string.IsNullOrEmpty(payload.Name) ? null : payload.Name;
        }

        private static IEnumerator ScanLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(SilentWatcherFeature.CheckInterval);
                try
                {
                    Scan();
                }
                catch (Exception ex)
                {
                    Log.Error("SilentWatcher", $"扫描失败：{ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static void Scan()
        {
            if (!Engine.Enabled<SilentWatcherFeature>())
                return;
            float now = Time.unscaledTime;
            if (_lastChatAt < 0f)
                return;
            float silent = now - _lastChatAt;
            if (silent < SilentWatcherFeature.TimeoutSeconds)
                return;
            if (SilentWatcherFeature.Cooldown > 0f
                && _lastTauntAt >= 0f && now - _lastTauntAt < SilentWatcherFeature.Cooldown)
                return;

            _lastTauntAt = now;
            string speaker = string.IsNullOrEmpty(_lastSpeaker) ? "某人" : _lastSpeaker;
            string line = Format(SilentWatcherFeature.TauntFormat, speaker, Mathf.FloorToInt(silent));
            if (!string.IsNullOrEmpty(line))
                Managers.Voice?.SendChatMessage(line);
        }

        /// <summary>{0}/{1} 占位替换；模板为空返回空串。</summary>
        private static string Format(string template, string arg0, int arg1)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;
            StringBuilder sb = new StringBuilder(template);
            sb.Replace("{0}", arg0).Replace("{1}", arg1.ToString());
            return sb.ToString();
        }
    }
}
