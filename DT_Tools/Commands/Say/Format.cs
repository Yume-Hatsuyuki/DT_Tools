using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Say
{
    /// <summary>/say 输出格式化：帮助文本、通道标签、人类回复 + JSON 结果 DTO。</summary>
    internal static class SayFormat
    {
        public const string HelpText =
            "━━━ /say 发送文字 ━━━\n" +
            "用法: /say <all|#id> <text>\n" +
            "\n" +
            "【通道自动路由】\n" +
            "  大厅/裁判 → normal（聊天按钮面板，PlayerId=0 无名字纯文字）\n" +
            "  生存阶段  → secret（黑/暗密聊屏幕浮层，需存活且非过期消息）\n" +
            "  调查/选角/总结算客户端无显示链路，会拒绝发送\n" +
            "\n" +
            "【目标】\n" +
            "  all          所有玩家（生存=所有存活真人，大厅/裁判=房间全员）——全员可见\n" +
            "  #<playerId>  仅该玩家可见（真·私密：其他黑/暗、白方、房主都收不到）\n" +
            "\n" +
            "【示例】\n" +
            "  /say all 这是一条广播通知。\n" +
            "  /say #2 你去做掉 #1（只有#2能看到这条）\n" +
            "  /say #3 你去做掉 #2（其他黑方/黑幕都看不到）\n" +
            "\n" +
            "【说明】文本经 SanitizeChat 过滤（去富文本、截断 100 字）；生存阶段的 Time 自动取当前生存时间。";

        public static string ChannelLabel(EChatType channel)
            => channel == EChatType.SecretChat ? "秘密聊天 secret" : "普通聊天 normal";

        private static string ChannelKey(EChatType channel)
            => channel == EChatType.SecretChat ? "secret" : "normal";

        public static object ResultAll(EChatType channel, int sent, string text)
            => new { mode = "all", channel = ChannelKey(channel), count = sent, text = text ?? "" };

        public static object ResultSingle(EChatType channel, Server.Game.Player target, string text)
            => new
            {
                mode = "single",
                channel = ChannelKey(channel),
                pid = target.PublicInfo.PlayerId,
                name = target.Name ?? "",
                text = text ?? "",
            };
    }
}
