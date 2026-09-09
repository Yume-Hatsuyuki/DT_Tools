using System.Collections.Concurrent;
using HarmonyLib;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   VoiceManager::EnqueueNormalChat(int playerId, string message, bool isDeadByHost)
    ///
    /// <b>原版效果</b>：
    ///   发送者非死亡 → 入 _messageQueue（全员可见）。
    ///   发送者死亡 → 仅当本地玩家也死亡时入 _deadMessageQueue；活人直接丢弃。
    ///
    /// <b>修改后效果</b>：
    ///   发送者死亡时，无论本地是否存活，一律入 _deadMessageQueue。
    ///   活人因此也能看到死者聊天（仍走死聊分发路径，UI 可区分）。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 按原版构造 ChatPayload，放宽死亡消息入队条件后写回队列并跳过原方法。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.EnqueueNormalChat))]
    [PatchConfig(
        "EnqueueNormalChat",
        "亡者呢喃：那些死者的回响依附在你的身边。\n启用后活人也能看到死者的聊天消息。",
        author: "梦初雪")]
    internal static class Patch_EnqueueNormalChat
    {
        [HarmonyPrefix]
        private static bool Prefix(VoiceManager __instance, int playerId, string message, bool isDeadByHost)
        {
            Player player = Managers.Player?.GetPlayerCache(playerId);
            var item = new VoiceManager.ChatPayload
            {
                PlayerId = playerId,
                Name = player != null ? player.DisplayName : string.Empty,
                Message = message
            };

            bool senderDead = isDeadByHost
                || (Managers.Player != null && Managers.Player.KnownDeadIds.Contains(playerId));

            var traverse = Traverse.Create(__instance);

            if (!senderDead)
            {
                var messageQueue = traverse.Field("_messageQueue")
                    .GetValue<ConcurrentQueue<VoiceManager.ChatPayload>>();
                messageQueue?.Enqueue(item);
            }
            else
            {
                // 原版：仅本地也死亡时入队；此处始终入队，使活人可见死聊
                var deadMessageQueue = traverse.Field("_deadMessageQueue")
                    .GetValue<ConcurrentQueue<VoiceManager.ChatPayload>>();
                deadMessageQueue?.Enqueue(item);
            }

            return false;
        }
    }
}
