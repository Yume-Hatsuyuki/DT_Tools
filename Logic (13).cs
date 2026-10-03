using System.Collections.Generic;
using System.Text;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Patches.Experience.TalkPolice
{
    /// <summary>
    /// 发言监督员的核心逻辑（普通类，非 Harmony 补丁）。
    /// 订阅 OnChatReceived 做滑动窗口计数，话痨超阈值点名广播；
    /// 每轮计票（OnCountingVote）时播报话痨之王并清空计数。
    /// </summary>
    internal static class TalkPoliceLogic
    {
        private static bool _subscribed;
        private static float _lastTauntAt;

        /// <summary>playerId → (窗口内发言次数, 窗口起始时间)；窗口过期即重置。</summary>
        private static readonly Dictionary<int, Counter> Counters = new Dictionary<int, Counter>();

        /// <summary>本窗口内已被点名过的玩家（每窗口每玩家最多点名一次，防刷屏）。</summary>
        private static readonly HashSet<int> NamedThisWindow = new HashSet<int>();

        private sealed class Counter
        {
            public int Count;
            public float WindowStart;
        }

        public static void EnsureSubscribed(VoiceManager voice)
        {
            if (_subscribed)
                return;
            if (!Engine.Enabled<TalkPoliceFeature>())
                return;
            _subscribed = true;
            voice.OnChatReceived += OnChatReceived;
        }

        private static void OnChatReceived(VoiceManager.ChatPayload payload, bool isDead)
        {
            if (!Engine.Enabled<TalkPoliceFeature>())
                return;
            if (Managers.Player == null)
                return;
            if (TalkPoliceFeature.IgnoreSelf && payload.PlayerId == Managers.Player.MyPlayerID)
                return;
            if (string.IsNullOrEmpty(payload.Message))
                return;

            float now = Time.unscaledTime;
            if (!Counters.TryGetValue(payload.PlayerId, out Counter counter)
                || now - counter.WindowStart > TalkPoliceFeature.WindowSeconds)
            {
                counter = new Counter { Count = 1, WindowStart = now };
                Counters[payload.PlayerId] = counter;
                NamedThisWindow.Remove(payload.PlayerId);
            }
            else
            {
                counter.Count++;
            }

            if (counter.Count < TalkPoliceFeature.Threshold)
                return;
            if (NamedThisWindow.Contains(payload.PlayerId))
                return;
            if (TalkPoliceFeature.Cooldown > 0f && now - _lastTauntAt < TalkPoliceFeature.Cooldown)
                return;

            _lastTauntAt = now;
            NamedThisWindow.Add(payload.PlayerId);
            string name = string.IsNullOrEmpty(payload.Name) ? "玩家" + payload.PlayerId : payload.Name;
            string line = Format(TalkPoliceFeature.TauntFormat, name, counter.Count);
            if (!string.IsNullOrEmpty(line))
                Managers.Voice?.SendChatMessage(line);
        }

        public static void OnCountingVote()
        {
            if (!Engine.Enabled<TalkPoliceFeature>())
                return;
            if (string.IsNullOrEmpty(TalkPoliceFeature.SummaryFormat))
            {
                Counters.Clear();
                NamedThisWindow.Clear();
                return;
            }

            int bestId = -1;
            int bestCount = 0;
            foreach (var pair in Counters)
            {
                if (pair.Value.Count > bestCount)
                {
                    bestCount = pair.Value.Count;
                    bestId = pair.Key;
                }
            }
            if (bestId >= 0 && bestCount > 0)
            {
                string name = PlayerName(bestId);
                string line = Format(TalkPoliceFeature.SummaryFormat, name, bestCount);
                if (!string.IsNullOrEmpty(line))
                    Managers.Voice?.SendChatMessage(line);
            }

            Counters.Clear();
            NamedThisWindow.Clear();
        }

        private static string PlayerName(int playerId)
        {
            var player = Managers.Player?.GetPlayerCache(playerId);
            if (player != null && !string.IsNullOrEmpty(player.DisplayName))
                return player.DisplayName;
            return "玩家" + playerId;
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
