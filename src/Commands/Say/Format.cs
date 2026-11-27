// 作用：格式化 /Say 命令的回复文本；不负责命令执行与注册。
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Say
{
    internal static class SayFormat
    {
        public const string HelpText =
            "━━━ /say 发送文字 ━━━\n" +
            "用法: /say <all|#id> <text>\n" +
            "\n" +
            "【通道自动路由】\n" +
            "  大厅/庭审 → normal（聊天面板，显示发送者）\n" +
            "  生存/调查 → radio（电台，仅存活黑/暗可见，只保留最新一条）\n" +
            "\n" +
            "【目标】\n" +
            "  all          电台：存活的黑/暗玩家；大厅/庭审：房间全员\n" +
            "  #<playerId>  指定玩家；电台目标必须是存活的黑/暗\n" +
            "\n" +
            "【示例】\n" +
            "  /say all 集合，准备开工。\n" +
            "  /say #2 去检查电闸。\n" +
            "\n" +
            "【说明】文本经 SanitizeChat 过滤（去富文本、截断 100 字）；发送者标识使用房主当前玩家。";

        public static string ChannelLabel(EChatType channel)
            => channel == EChatType.SecretChat ? "电台 radio" : "普通聊天 normal";

        private static string ChannelKey(EChatType channel)
            => channel == EChatType.SecretChat ? "radio" : "normal";

        private static bool LatestOnly(EChatType channel)
            => channel == EChatType.SecretChat;

        public static object ResultAll(EChatType channel, int sent, string text)
            => new
            {
                mode = "all",
                channel = ChannelKey(channel),
                count = sent,
                latestOnly = LatestOnly(channel),
                audience = LatestOnly(channel) ? "alive-dark-black" : "room",
                text = text ?? "",
            };

        public static object ResultSingle(EChatType channel, Server.Game.Player target, string text)
            => new
            {
                mode = "single",
                channel = ChannelKey(channel),
                latestOnly = LatestOnly(channel),
                pid = target.PublicInfo.PlayerId,
                name = target.Name ?? "",
                text = text ?? "",
            };
    }
}
