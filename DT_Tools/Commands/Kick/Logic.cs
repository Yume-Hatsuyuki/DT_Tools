using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Kick
{
    internal static class KickLogic
    {
        public static bool TryGuard(GameRoom room, out string code, out string text)
        {
            if (room.State != EGameState.Lobby)
            {
                code = "lobby only";
                text = "只能在大厅（Lobby）状态下踢人。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }

        public static bool TryResolveTarget(GameRoom room, int targetId, out Server.Game.Player target, out string code, out string text)
        {
            var host = room.Host;
            if (host?.PublicInfo == null)
            {
                target = null;
                code = "host missing";
                text = "无法获取房主玩家对象。";
                return false;
            }
            if (host.PublicInfo.PlayerId == targetId)
            {
                target = null;
                code = "self kick";
                text = "不能踢自己。";
                return false;
            }
            target = PlayerQuery.FindById(room, targetId);
            if (target == null)
            {
                code = "target not found";
                text = $"找不到 PlayerId={targetId} 的玩家。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }
    }
}
