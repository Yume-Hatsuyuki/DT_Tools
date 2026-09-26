using DT_Tools.Commands;
using Server.Game;

namespace DT_Tools.Commands.ListAlive
{
    /// <summary>
    /// /list_alive — 列出当前仍存活的玩家，并标注阵营（黑幕 / 黑方 / 白方）与「你」。
    /// 仅房主、仅游戏中（选角结束之后）；完整阵营数据仅在 Host 端。
    /// </summary>
    internal sealed class ListAliveCommand : ICommand
    {
        public string Name => "list_alive";
        public string[] Aliases => new[] { "alive", "存活", "存活人数" };
        public string Usage => "list_alive";
        public string Description => "列出存活玩家并标注阵营（仅游戏中、需房主）。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }
            if (!ListAliveLogic.IsInGame(room.State))
            {
                ctx.Reply($"仅在游戏中可用（当前状态: {room.State}）。");
                return CommandResult.Fail("not in game", new { state = room.State.ToString() });
            }
            if (Managers.Player == null)
            {
                ctx.Reply("玩家管理器尚未初始化。");
                return CommandResult.Fail("player manager not ready");
            }

            int myId = Managers.Player.MyPlayerID;
            var alive = ListAliveLogic.Collect(room);

            ctx.Reply(ListAliveFormat.Reply(alive, myId));
            return CommandResult.Success(ListAliveFormat.Result(alive));
        }
    }
}
