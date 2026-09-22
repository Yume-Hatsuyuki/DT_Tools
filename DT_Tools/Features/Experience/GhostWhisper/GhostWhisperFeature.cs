using System.Collections.Concurrent;
using HarmonyLib;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 活人可见死聊。原版仅本地也死亡时入 _deadMessageQueue；此处发送者死亡则一律入队。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.EnqueueNormalChat))]
    [PatchFeature(
        section: "EnqueueNormalChat",
        description: "亡者呢喃：那些死者的回响依附在你的身边。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class GhostWhisperFeature
    {
        [HarmonyPrefix]
        private static bool Prefix(VoiceManager __instance, int playerId, string message, bool isDeadByHost)
        {
            if (!FeatureGate.Enabled(typeof(GhostWhisperFeature)))
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
