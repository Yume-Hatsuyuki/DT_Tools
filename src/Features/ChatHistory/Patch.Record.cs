// 作用：为聊天历史导航挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.ChatHistory
{
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
}
