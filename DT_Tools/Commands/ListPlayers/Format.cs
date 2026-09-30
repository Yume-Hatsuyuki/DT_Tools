using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DT_Tools.Commands.ListPlayers
{
    /// <summary>/list_players 输出格式化：玩家列表文本 + JSON 结果 DTO。</summary>
    internal static class ListPlayersFormat
    {
        public static string Reply(List<ListPlayersLogic.Entry> players, int hostId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 玩家列表 ━━━");

            foreach (var e in players)
            {
                string steamStr = e.SteamId != 0 ? e.SteamId.ToString() : "未知";

                string tag = "";
                if (e.IsHost && e.IsSelf) tag = "  ← 房主·你";
                else if (e.IsHost)        tag = "  ← 房主";
                else if (e.IsSelf)        tag = "  ← 你";

                sb.AppendLine($"  #{e.Pid,-4} {e.Name,-16} steam_id={steamStr}{tag}");
            }

            if (hostId == 0)
                sb.AppendLine("（未能识别房主）");

            sb.Append($"共 {players.Count} 名玩家");
            return sb.ToString();
        }

        public static object Result(List<ListPlayersLogic.Entry> players, int hostId, int myId)
            => new
            {
                count = players.Count,
                hostId,
                myId,
                players = players.Select(e => new
                {
                    playerId = e.Pid,
                    name = e.Name ?? "",
                    steamId = e.SteamId != 0 ? e.SteamId.ToString() : "",
                    isHost = e.IsHost,
                    isSelf = e.IsSelf,
                }),
            };
    }
}
