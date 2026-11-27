// 作用：为通灵挂载补丁拦截；不负责配置声明与状态存储。
using System.Collections.Concurrent;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.SpiritChannel
{
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
