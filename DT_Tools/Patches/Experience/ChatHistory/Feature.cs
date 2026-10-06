using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.ChatHistory
{
    /// <summary>
    /// 聊天历史导航（客户端）：聊天快捷条（大厅、对局昼夜、庭审）、庭审平板聊天、打字机（对讲机）
    /// 输入框支持 ↑/↓ 键翻看并回填本机发送过的消息。三个输入面各有子开关；
    /// 打字机公开频道与秘密频道历史分开，且不与全局聊天（快捷条/平板）混用。
    /// 挂载双保险：Init 补丁在对象初始化时即时挂载，另有常驻兜底挂载器节流扫描补漏
    /// （大厅启动场景的 UI Awake 可能早于补丁挂载，Init 补丁会错过）。热关闭由 OnDisabled
    /// 清理宿主与导航器，热重开由 OnPatched/兜底扫描重新挂载。
    /// </summary>
    [PatchFeature(
        "聊天历史导航：大厅/对局/庭审聊天条、庭审平板聊天、打字机对讲机输入框支持 ↑/↓ 键翻看并回填本机发送过的消息" +
        "（三个输入面子开关独立；打字机公开与秘密频道历史分开、不与聊天条混用；相邻重复自动跳过；" +
        "输入法组词中不劫持方向键。需先发送过消息，空历史无反应）。关闭即恢复原版。",
        defaultEnabled: false,
        side: FeatureSide.Client,
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

        /// <summary>启动读档与热重开路径：重建兜底挂载宿主（AGENTS.md §8）。</summary>
        private static void OnPatched()
        {
            if (Engine.Enabled<ChatHistoryFeature>())
                ChatHistoryMounter.EnsureHost();
        }

        /// <summary>热关闭：销毁兜底宿主并移除导航器，输入框恢复原版（AGENTS.md §8）。</summary>
        private static void OnDisabled()
        {
            ChatHistoryMounter.Shutdown();
        }
    }
}
