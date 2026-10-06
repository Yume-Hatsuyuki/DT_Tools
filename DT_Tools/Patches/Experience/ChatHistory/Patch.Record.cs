using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.ChatHistory
{
    /// <summary>
    /// 打字机公开频道记录点：SendDeviceChatMessage 无本地拒绝分支（0.1.16b VoiceManager.cs:1298
    /// 直接 SendChatPacket），Postfix 即实际发出。打字机历史独立成池，不与全局聊天混用。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.SendDeviceChatMessage))]
    internal static class ChatHistoryRecordInterphoneOpenPatch
    {
        private static void Postfix(string message)
        {
            if (!Engine.Enabled<ChatHistoryFeature>())
                return;
            ChatHistoryStore.Record(ChatHistoryChannel.InterphoneOpen, message);
        }
    }

    /// <summary>
    /// 打字机秘密频道记录点：SendSecretChatMessage 冷却未就绪时静默拒绝不发包
    /// （0.1.16b VoiceManager.cs:1305-1309），故 Prefix 存 IsSecretChatReady 快照、
    /// Postfix 仅在就绪时记录，避免把被拒消息写进历史。IsSecretChatReady 为公开属性
    /// （0.1.16b VoiceManager.cs:254，UI_ChatDevicePopup.cs:278 亦调用）。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.SendSecretChatMessage))]
    internal static class ChatHistoryRecordInterphoneSecretPatch
    {
        private static void Prefix(VoiceManager __instance, out bool __state)
        {
            __state = Engine.Enabled<ChatHistoryFeature>() && __instance.IsSecretChatReady;
        }

        private static void Postfix(string message, bool __state)
        {
            if (!__state)
                return;
            ChatHistoryStore.Record(ChatHistoryChannel.InterphoneSecret, message);
        }
    }
}
