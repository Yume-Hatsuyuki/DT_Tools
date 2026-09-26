using System.Collections.Generic;
using System.Linq;
using DT_Tools.Commands;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Faction
{
    /// <summary>
    /// /list_black、/list_dark、/list_white 共用业务：按 PlayerId 升序列出指定阵营玩家
    /// （房主门禁由框架 RequireHost 统一执行，完整阵营数据仅在 Host 端）。
    /// </summary>
    internal static class FactionLogic
    {
        /// <summary>非大厅的进行中状态（选角结束之后）。</summary>
        private static bool IsInGame(EGameState state)
        {
            return state != EGameState.Lobby
                && state != EGameState.NoneState
                && state != EGameState.PickCharacter;
        }

        public static CommandResult Execute(CommandContext ctx, EPlayerColor color, string title)
        {
            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }

            if (!IsInGame(room.State))
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

            // 用 var：程序集里另有客户端 Player，裸写 List<Player> 会和 Server.Game.Player 冲突（CS0029）
            var matched = room.Players
                .Where(p => p?.PublicInfo != null
                    && !p.IsSpectator
                    && !p.IsDummy
                    && p.Color == color)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

            ctx.Reply(FactionFormat.Reply(title, matched, myId));
            return CommandResult.Success(FactionFormat.Result(color, matched));
        }
    }
}
