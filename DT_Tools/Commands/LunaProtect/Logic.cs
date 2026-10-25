using System.Collections.Generic;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.LunaProtect
{
    internal static class LunaProtectLogic
    {
        public static bool TryGuard(GameRoom room, out string code, out string text)
        {
            if (!RoomFlow.TryGuardBusy(room, "施加护盾", out code, out text))
                return false;
            if (room.State != EGameState.Survive)
            {
                code = "invalid state";
                text = $"护盾命令仅能在生存阶段（Survive）使用，当前状态: {room.State}。";
                return false;
            }
            return true;
        }

        public static void Apply(GameRoom room, IEnumerable<Server.Game.Player> targets)
        {
            foreach (var player in targets)
            {
                room.Broadcast(new S_NOTIFY_LUNA_ABILITY
                {
                    PlayerId = player.PublicInfo.PlayerId,
                });
            }
        }

        public static bool IsTargetProtectable(Server.Game.Player target, int targetId, out string code, out string text)
        {
            if (target.IsSpectator)
            {
                code = "target is spectator";
                text = $"目标 {target.Name}（#{targetId}）是观战者，护盾无意义。";
                return false;
            }
            if (!target.IsAlive)
            {
                code = "target already dead";
                text = $"目标 {target.Name}（#{targetId}）已死亡，无法套用护盾。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }
    }
}
