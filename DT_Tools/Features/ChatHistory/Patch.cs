using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.ChatHistory
{
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
