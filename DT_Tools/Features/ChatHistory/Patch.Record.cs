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
