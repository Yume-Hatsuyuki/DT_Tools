using System.Collections.Concurrent;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.SpiritChannel
{
    /// <summary>
    /// 亡者呢喃：EnqueueNormalChat 整替。
    /// 入队规则改为「发送者死亡 → _deadMessageQueue（不论本地生死），发送者存活 → _messageQueue」。
    /// 私有队列字段字符串定位：_messageQueue 0.1.16b VoiceManager.cs:131、
    /// _deadMessageQueue :133；ChatPayload 结构体 :35。
    /// 公共成员用 nameof：EnqueueNormalChat、GetPlayerCache（PlayerManager.cs:193）、
    /// KnownDeadIds（PlayerManager.cs:56）、DisplayName。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.EnqueueNormalChat))]
    internal static class SpiritChannelGhostWhisperPatch
    {
        private static bool Prefix(VoiceManager __instance, int playerId, string message, bool isDeadByHost)
        {
            if (!Engine.Enabled<SpiritChannelFeature>() || !SpiritChannelFeature.GhostWhisper)
                return true;

            Player player = Managers.Player?.GetPlayerCache(playerId);
            var item = new VoiceManager.ChatPayload
            {
                PlayerId = playerId,
                Name = player != null ? player.DisplayName : string.Empty,
                Message = message
            };

            bool senderDead = isDeadByHost
                || (Managers.Player != null && Managers.Player.KnownDeadIds.Contains(playerId));

            var t = Traverse.Create(__instance);
            if (!senderDead)
                t.Field("_messageQueue").GetValue<ConcurrentQueue<VoiceManager.ChatPayload>>()?.Enqueue(item);
            else
                t.Field("_deadMessageQueue").GetValue<ConcurrentQueue<VoiceManager.ChatPayload>>()?.Enqueue(item);

            return false;
        }
    }
}
