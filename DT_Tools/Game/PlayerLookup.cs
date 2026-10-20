using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Game
{
    /// <summary>
    /// 房主侧玩家查找：按玩家 ID（数字）、账号名（精确）或 AccountID 定位。
    /// </summary>
    /// <remarks>
    /// 名字重复时拒绝匹配并要求改用 ID，避免误改另一名玩家。
    /// 玩家类型为服务端 Server.Game.Player（0.1.18c Server.Game/GameRoom.cs:47 Players 列表）。
    /// </remarks>
    public static class PlayerLookup
    {
        /// <summary>
        /// 在候选玩家中定位目标。成功时 player 有值；失败时 error 给出中文原因。
        /// </summary>
        public static bool TryResolve(IEnumerable<Server.Game.Player> players, string key,
            out Server.Game.Player player, out string error)
        {
            player = null;
            error = null;
            string k = (key ?? string.Empty).Trim();
            if (k.Length == 0)
            {
                error = "玩家名或 ID 不能为空。";
                return false;
            }

            var list = (players ?? Enumerable.Empty<Server.Game.Player>())
                .Where(p => p?.PublicInfo != null)
                .ToList();

            var byId = list.Where(p => p.PublicInfo.PlayerId.ToString() == k || p.AccountID == k).ToList();
            if (byId.Count == 1)
                return Accept(list, byId[0], out player, out error);

            var byName = list.Where(p => p.Name == k).ToList();
            if (byName.Count == 1)
                return Accept(list, byName[0], out player, out error);
            if (byName.Count > 1)
            {
                error = $"「{k}」对应多名玩家，请改用玩家 ID。";
                return false;
            }

            error = $"未找到玩家「{k}」。";
            return false;
        }

        /// <summary>
        /// 账号标识（AccountID）与他人相同时拒绝。假人的 AccountID 取其名字（Game/FakePlayers.cs:126），
        /// 同名假人共用标识，按标识设置的权重或覆盖会串到另一人身上。
        /// </summary>
        private static bool Accept(System.Collections.Generic.List<Server.Game.Player> list, Server.Game.Player target,
            out Server.Game.Player player, out string error)
        {
            player = null;
            error = null;
            if (list.Count(p => p.AccountID == target.AccountID) > 1)
            {
                error = $"「{target.Name}」与其他玩家的账号标识相同（假人同名），无法单独设置。";
                return false;
            }
            player = target;
            return true;
        }
    }
}
