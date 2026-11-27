// 作用：为打字机消息浮文化挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.TypewriterFloat
{
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.EnqueueDeviceChat))]
    internal static class TypewriterFloatDeviceChatPatch
    {
        private static void Postfix(int time, string message)
        {
            if (!Engine.Enabled<TypewriterFloatFeature>())
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
