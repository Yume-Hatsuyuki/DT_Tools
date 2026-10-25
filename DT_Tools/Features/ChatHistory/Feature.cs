using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ChatHistory
{
    [PatchFeature(
        "聊天历史导航：大厅/对局/庭审聊天条、庭审平板聊天、打字机对讲机输入框支持 ↑/↓ 键翻看并回填本机发送过的消息" +
        "（三个输入面子开关独立；打字机公开与秘密频道历史分开、不与聊天条混用；相邻重复自动跳过；" +
        "输入法组词中不劫持方向键。需先发送过消息，空历史无反应）。关闭即恢复原版。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiChat | FeatureTag.UiInput,
        Author = "梦初雪")]
    public sealed class ChatHistoryFeature
    {
        [Config("聊天快捷条：大厅、对局昼夜与庭审的屏幕聊天条启用 ↑/↓ 历史（全局频道，与平板共用历史）。")]
        public static bool ChatBar = true;

        [Config("庭审平板：平板聊天页签输入框启用 ↑/↓ 历史（全局频道，与聊天条共用历史）。")]
        public static bool Tablet = true;

        [Config("打字机对讲机：对讲机输入框启用 ↑/↓ 历史（公开频道与秘密频道各存一份，不与聊天条混用）。")]
        public static bool Typewriter = true;

        [Config("历史条数上限：每个频道各自保留的最近发送消息条数，浏览从最新往最旧翻。", Min = 10f, Max = 500f)]
        public static int MaxHistory = 50;

        private static void OnPatched()
        {
            if (Engine.Enabled<ChatHistoryFeature>())
                ChatHistoryMounter.EnsureHost();
        }

        private static void OnDisabled()
        {
            ChatHistoryMounter.Shutdown();
        }
    }
}
