using DT_Tools.Commands;

namespace DT_Tools.Commands.RoomList
{
    /// <summary>
    /// /room_list — 列出当前所有公开房间（Steam Lobby 异步查询）。
    ///
    /// 注意：查询是异步回调，命令立即返回；人类文本经 ctx.Reply 在回调中输出。
    /// 旧 /api/run 的 SetResult 回写机制对异步命令需要由集成者在 WebConsole 阶段
    /// 单独处理（见集成注意事项）。
    /// </summary>
    internal sealed class RoomListCommand : ICommand
    {
        public string Name => "room_list";
        public string[] Aliases => new[] { "rooms", "list_rooms", "lobbies" };
        public string Usage => "room_list";
        public string Description => "列出当前所有公开房间（异步；日志 + JSON 回调）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!RoomListLogic.TryGetLobby(out var lobby, out string errCode, out string errText))
            {
                ctx.Reply(errText);
                return CommandResult.Fail(errCode, new { rooms = new object[0] });
            }

            ctx.Reply("正在查询公开房间列表...");

            RoomListLogic.Request(lobby, rooms =>
            {
                if (rooms == null || rooms.Count == 0)
                {
                    ctx.Reply("当前没有公开房间。");
                    return;
                }
                ctx.Reply(RoomListFormat.Reply(rooms));
            });

            // 异步命令：机器结果先返回"已受理"，列表在回调中经日志通道回写
            return CommandResult.Success(new { async = true });
        }
    }
}
