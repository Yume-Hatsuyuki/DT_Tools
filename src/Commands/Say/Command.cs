// 作用：实现 /say 命令的参数解析与执行；不负责命令注册与其他命令。
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Say
{
    internal sealed class SayCommand : ICommand
    {
        public string Name => "say";
        public string[] Aliases => new[] { "广播", "私聊" };
        public string Usage => "say <all|#id> <text>";
        public string Description => "给指定/所有玩家发送文字：生存/调查阶段走电台，大厅/庭审走聊天面板（房主）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.UiChat;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply(SayFormat.HelpText);
                return CommandResult.Success();
            }

            var room = GameRoom.Instance;
            if (room == null || room.Players == null || room.Players.Count == 0)
            {
                ctx.Reply("当前没有活动的游戏房间或玩家。");
                return CommandResult.Fail("no room");
            }

            if (!SayArgs.TryParse(ctx.Args, out var args, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("invalid target");
            }

            if (ctx.Args.Length < 2)
            {
                ctx.Reply($"缺少文字内容。用法: {Usage}");
                return CommandResult.Fail("missing text");
            }

            string text = room.SanitizeChat(args.Text);
            if (string.IsNullOrEmpty(text))
            {
                ctx.Reply("消息内容为空（或仅含被过滤的富文本/控制字符）。");
                return CommandResult.Fail("empty text");
            }

            if (!SayLogic.TryPickChannel(room.State, out var channel, out string channelCode, out string channelText))
            {
                ctx.Reply(channelText);
                return CommandResult.Fail(channelCode);
            }

            if (!LocalPlayer.TryGetPlayer(out var sender, out string senderError))
            {
                ctx.Reply($"无法获取发送者标识：{senderError}");
                return CommandResult.Fail("no sender");
            }

            var packet = SayLogic.BuildPacket(channel, text, sender.PublicInfo.PlayerId);

            if (args.All)
            {
                int sent = SayLogic.SendAll(room, channel, packet, ctx.Warn);
                if (channel == EChatType.SecretChat && sent == 0)
                {
                    ctx.Reply("电台没有可接收的存活黑/暗玩家，未发送。");
                    return CommandResult.Fail("no radio audience");
                }
                ctx.Reply(channel == EChatType.SecretChat
                    ? $"已发送（电台 radio，仅存活黑/暗，只保留最新一条）→ {sent} 名玩家。"
                    : $"已发送（普通聊天 normal）→ {sent} 名玩家。");
                return CommandResult.Success(SayFormat.ResultAll(channel, sent, text));
            }

            var found = PlayerQuery.FindById(room, args.PlayerId);
            if (found == null)
            {
                ctx.Reply($"找不到 PlayerId={args.PlayerId} 的玩家。");
                return CommandResult.Fail("target not found");
            }
            if (found.Session == null)
            {
                ctx.Reply($"玩家 {found.Name}（#{args.PlayerId}）无有效 Session。");
                return CommandResult.Fail("no session");
            }
            if (!SayLogic.SendOne(found, channel, packet, out string sendError))
            {
                ctx.Reply(sendError);
                return CommandResult.Fail("send failed");
            }
            ctx.Reply(channel == EChatType.SecretChat
                ? $"已发送（电台 radio，仅存活黑/暗，只保留最新一条）→ {found.Name}（#{args.PlayerId}）。"
                : $"已发送（普通聊天 normal）→ {found.Name}（#{args.PlayerId}）。");
            return CommandResult.Success(SayFormat.ResultSingle(channel, found, text));
        }
    }
}
