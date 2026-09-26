using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.ChatLimit
{
    /// <summary>
    /// 取消聊天输入框字数截断（客户端）：原版各聊天输入框硬编码 characterLimit = 100。
    /// 房主侧发言长度过滤是另一道独立闸门，见「ChatSanitize」（两处上限需配合）。
    /// </summary>
    [PatchFeature(
        "取消聊天输入框字数截断：原版输入框上限 100，开启后抬升上限（默认 1000，可改 MaxLength）。\n房主侧发言过滤为独立功能「ChatSanitize」，客户端上限高于其值时发言仍会被服务端截断。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class ChatLimitFeature
    {
        [Config("聊天输入框最大字数（范围 100～8000）。", Min = 100, Max = 8000)]
        public static int MaxLength = 1000;

        /// <summary>运行时打开：对当前已存在的输入框补一次扫描。</summary>
        private static void OnEnabled() => ChatLimitLogic.ApplyToExistingUi();
    }
}
