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
                string steamName = string.IsNullOrEmpty(e.SteamName) ? "未知" : e.SteamName;

                string tag = "";
                if (e.IsHost && e.IsSelf) tag = "  ← 房主·你";
                else if (e.IsHost)        tag = "  ← 房主";
                else if (e.IsSelf)        tag = "  ← 你";

                // #1 Alice [direct]ip_src=x.x.x.x:port steam_id=... steam_name=...
                string transport = e.Relayed == null ? "unknown" : (e.Relayed.Value ? "relay" : "direct");
                string ipStr = string.IsNullOrEmpty(e.RemoteIp) ? "—" : e.RemoteIp;
                string pingStr = e.PingMs >= 0 ? $" ping={e.PingMs}ms" : "";

                sb.AppendLine(
                    $"  #{e.Pid} {e.Name} [{transport}]ip_src={ipStr} steam_id={steamStr} steam_name={steamName}{pingStr}{tag}");
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
                    steamName = e.SteamName ?? "",
                    ip = e.RemoteIp ?? "",
                    relayed = e.Relayed,
                    pingMs = e.PingMs,
                    isHost = e.IsHost,
                    isSelf = e.IsSelf,
                }),
            };
    }
}
