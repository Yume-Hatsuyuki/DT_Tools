using DT_Tools.Commands;
using DT_Tools.Game;
using Server.Game;

namespace DT_Tools.Commands.Say
{
    /// <summary>
    /// /say &lt;all|#id&gt; &lt;text&gt; — 给指定玩家或所有玩家发送文字（需房主）。
    ///
    /// 绕过原版聊天校验与冷却，直接向目标客户端下发 S_CHAT_MESSAGE，复用游戏自带的
    /// 文字显示链路。通道按当前阶段自动路由：Lobby/Trial → normal（聊天面板）、
    /// Survive → secret（密聊屏幕浮层）、其余阶段无显示链路直接拒绝。
    /// all 在生存阶段发所有存活真人（对齐 AlivePlayers）、大厅/裁判发房间全员；
    /// #id 为真·私密单发（其他黑/暗、白方、房主均收不到，不进 SecretChatLog）。
    /// </summary>
    internal sealed class SayCommand : ICommand
    {
        public string Name => "say";
        public string[] Aliases => new[] { "广播", "私聊" };
        public string Usage => "say <all|#id> <text>";
        public string Description => "给指定/所有玩家发送文字：生存阶段走密聊浮层，大厅/裁判走聊天面板（房主）。";
        public string Author => "梦初雪";

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
                ctx.Reply("缺少文字内容。用法: " + Usage);
                return CommandResult.Fail("missing text");
            }

            // 文本经 GameRoom.SanitizeChat 过滤（去富文本、截断 100 字），与游戏聊天一致
            // （0.1.15b Server.Game/GameRoom.cs:2524）
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

            var packet = SayLogic.BuildPacket(channel, text);

            if (args.All)
            {
                int sent = SayLogic.SendAll(room, channel, packet, ctx.Warn);
                ctx.Reply($"已发送（{SayFormat.ChannelLabel(channel)}）→ {sent} 名玩家。");
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
            if (!SayLogic.SendOne(found, packet, out string sendError))
            {
                ctx.Reply($"发送失败: {sendError}");
                return CommandResult.Fail("send failed");
            }
            ctx.Reply($"已发送（{SayFormat.ChannelLabel(channel)}）→ {found.Name}（#{args.PlayerId}）。");
            return CommandResult.Success(SayFormat.ResultSingle(channel, found, text));
        }
    }
}
