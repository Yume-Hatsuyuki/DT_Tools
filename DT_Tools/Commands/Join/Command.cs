using DT_Tools.Commands;
using DT_Tools.Core;

namespace DT_Tools.Commands.Join
{
    /// <summary>
    /// /join &lt;房间码&gt; — 按房间码进入其他房间（任何阶段可用）。
    ///
    /// 在房内时先走原版退出路径（Network.Leave + 回大厅场景；若自己是房主，
    /// 房间按原版规则移交/解散），等大厅 UI 就绪后按码搜索并复用原版进房编排。
    /// 目标房间正在对局 → 以观战进入。加入结果经 [Join] 段日志与游戏内提示反馈。
    /// </summary>
    internal sealed class JoinCommand : ICommand
    {
        public string Name => "join";
        public string[] Aliases => new[] { "进入房间" };
        public string Usage => "join <房间码>";
        public string Description => "按房间码进入其他房间（支持在对局中使用，将先退出当前房间）。目标房间在对局中则以观战进入。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /join <房间码>");
                return CommandResult.Fail("missing code");
            }
            if (!JoinArgs.TryParseCode(ctx.Args[0], out string code, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("invalid code");
            }

            bool wasInRoom = JoinLogic.IsInRoom;
            if (wasInRoom)
            {
                JoinLogic.LeaveCurrentRoom();
                ctx.Reply($"已离开当前房间，正在加入房间码 {code} …（若你是房主，原房间将按原版规则移交或解散）");
            }
            else
            {
                ctx.Reply($"正在加入房间码 {code} …");
            }
            JoinLogic.StartJoinFlow(code);
            return CommandResult.Success(new { code, leftRoom = wasInRoom });
        }
    }
}
