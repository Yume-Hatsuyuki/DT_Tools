using System.Collections.Generic;
using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Faction
{
    internal static class FactionLogic
    {
        public static CommandResult Execute(CommandContext ctx, EPlayerColor color, string title)
        {
            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }

            if (!global::DT_Tools.Game.GameState.IsInGame(room.State))
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

            var matched = room.Players
                .Where(p => p?.PublicInfo != null
                    && !p.IsSpectator
                    && p.Color == color)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

            ctx.Reply(FactionFormat.Reply(title, matched, myId));
            return CommandResult.Success(FactionFormat.Result(color, matched));
        }
    }
}
