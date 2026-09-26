using DT_Tools.Commands;

namespace DT_Tools.Commands.RoomCode
{
    /// <summary>/room_code — 显示当前房间的房间号（邀请码）及玩家数。</summary>
    internal sealed class RoomCodeCommand : ICommand
    {
        public string Name => "room_code";
        public string[] Aliases => new[] { "code", "room" };
        public string Usage => "room_code";
        public string Description => "显示当前房间的房间号（邀请码）及玩家数。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!RoomCodeLogic.TryRead(
                    out string code, out bool isHost, out int current, out int max, out int steamLimit,
                    out string errCode, out string errText))
            {
                ctx.Reply(errText);
                return errCode == "not in room"
                    ? CommandResult.Fail(errCode, new { code = "" })
                    : CommandResult.Fail(errCode);
            }

            ctx.Reply(RoomCodeFormat.Reply(code, current, max, steamLimit));
            return CommandResult.Success(RoomCodeFormat.Result(code, current, max, isHost, steamLimit));
        }
    }
}
