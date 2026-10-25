using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Game
{
    public static class PlayerLookup
    {
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
