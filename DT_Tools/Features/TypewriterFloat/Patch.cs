using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.TypewriterFloat
{
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.EnqueueDeviceChat))]
    internal static class TypewriterFloatDeviceChatPatch
    {
        private static void Postfix(int time, string message, bool isSecret)
        {
            if (!Engine.Enabled<TypewriterFloatFeature>())
                return;
            if (isSecret)
                return;
            if (string.IsNullOrEmpty(message))
                return;

            TypewriterFloatLogic.OnDeviceChat(time, message);
        }
    }

    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.SendDeviceChatMessage))]
    internal static class TypewriterFloatSelfSendPatch
    {
        private static void Prefix(string message)
        {
            TypewriterFloatLogic.MarkSelfSend(message);
        }
    }
}
