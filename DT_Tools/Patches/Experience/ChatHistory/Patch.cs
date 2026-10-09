using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.ChatHistory
{
    /// <summary>
    /// 记录点（全局聊天）：VoiceManager.SendChatMessage 是全局聊天的唯一出口——聊天快捷条
    /// （0.1.16b UI_DirectChat.cs:273）与庭审平板（0.1.16b UI_GameTablet.cs:2644）都调用它，
    /// 在此记录可保证两条 UI 的历史一致（共用一份全局池）。Postfix 时机 = 消息已确定发出。
    /// 打字机两频道记录在 Patch.Record.cs（独立通道）；PhotoSend 的「![文字](路径)」文字
    /// 先发也入历史，翻出后重发等于重发该照片。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.SendChatMessage))]
    internal static class ChatHistoryRecordPatch
    {
        private static void Postfix(string message)
        {
            if (!Engine.Enabled<ChatHistoryFeature>())
                return;
            ChatHistoryStore.Record(ChatHistoryChannel.GlobalChat, message);
        }
    }
}
