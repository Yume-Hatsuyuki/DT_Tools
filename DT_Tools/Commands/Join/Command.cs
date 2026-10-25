using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Core;
using DT_Tools.Game;

namespace DT_Tools.Commands.Join
{
    internal sealed class JoinCommand : ICommand
    {
        public string Name => "join";
        public string[] Aliases => new[] { "进入房间" };
        public string Usage => "join <房间码>";
        public string Description => "按房间码进入其他房间（支持在对局中使用，将先退出当前房间）。目标房间在对局中则以观战进入。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.SysRoom;
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

            bool wasInRoom = RoomFlow.IsInRoom;
            if (wasInRoom)
            {
                RoomFlow.LeaveCurrentRoom();
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
