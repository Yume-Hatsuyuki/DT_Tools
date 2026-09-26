using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Kick
{
    /// <summary>/kick 业务：大厅门禁、目标校验（房主门禁由框架按 RequireHost 统一执行）。</summary>
    internal static class KickLogic
    {
        /// <summary>房间门禁：踢人仅在大厅可用（服务端 KickPlayer 只处理 Lobby）。</summary>
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

        /// <summary>目标校验：房主对象存在、不能踢自己、目标在房间内。</summary>
        public static bool TryResolveTarget(GameRoom room, int targetId, out Server.Game.Player target, out string code, out string text)
        {
            var host = room.Host;   // 0.1.15b Server.Game/GameRoom.cs:145
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
            // GameRoom.Players 按 PlayerId 查目标（收敛自 Game/PlayerQuery.FindById）
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
