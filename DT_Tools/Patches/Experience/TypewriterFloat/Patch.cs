using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.TypewriterFloat
{
    /// <summary>
    /// 打字机（对讲机）消息浮文化触发：
    /// - EnqueueDeviceChat 后缀（public，0.1.15b VoiceManager.cs:1353）——对讲机公开频道
    ///   （DeviceChat）消息改用秘密通话浮文样式匿名显示；秘密通话（isSecret=true）由
    ///   原版 UI_SecretChatOverlay 浮文，跳过以避免双份。
    /// - SendDeviceChatMessage 前缀（public，:1307）——记录本机发送的文本与时刻，
    ///   用于识别服务器回显的自己的公开消息（浮文只显示其他人的消息）。
    /// 消息只在对局生存阶段出现（服务端 RelayDeviceChat 仅 Survive 中继且发给存活玩家）。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.EnqueueDeviceChat))]
    internal static class TypewriterFloatDeviceChatPatch
    {
        private static void Postfix(int time, string message, bool isSecret)
        {
            if (!Engine.Enabled<TypewriterFloatFeature>())
                return;
            if (isSecret)
                return;    // 秘密通话：原版 UI_SecretChatOverlay 已浮文
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
