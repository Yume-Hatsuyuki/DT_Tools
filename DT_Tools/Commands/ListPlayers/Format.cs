using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DT_Tools.Commands.ListPlayers
{
    internal static class ListPlayersFormat
    {
        public static string Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine("用法: /list_players [参数…]");
            sb.AppendLine("每个参数显示一条信息，多个参数按写入顺序拼接；不带参数时显示本说明。");
            foreach (var row in ListPlayersArgs.Table)
                sb.AppendLine($"  {row.Token,-11}{row.Description}");
            sb.AppendLine($"  {ListPlayersArgs.AllToken,-11}以上全部");
            sb.Append("示例: /list_players id name character   →   #2 LUE Rin");
            return sb.ToString();
        }

        public static object HelpResult()
            => new
            {
                help = true,
                modules = ListPlayersArgs.Table.Select(r => r.Token).ToArray(),
            };

        public static string Reply(List<ListPlayersLogic.Entry> players, int hostId, IReadOnlyList<ListPlayersModule> modules)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 玩家列表 ━━━");

            foreach (var e in players)
            {
                string line = string.Join(" ", modules
                    .Select(m => Fragment(m, e))
                    .Where(f => !string.IsNullOrEmpty(f)));
                sb.AppendLine("  " + (line.Length > 0 ? line : "（无标注）"));
            }

            if (modules.Contains(ListPlayersModule.Role) && hostId == 0)
                sb.AppendLine("（未能识别房主）");

            sb.Append($"共 {players.Count} 名玩家");
            return sb.ToString();
        }

        private static string Fragment(ListPlayersModule module, ListPlayersLogic.Entry e)
        {
            switch (module)
            {
                case ListPlayersModule.Id:
                    return $"#{e.Pid}";
                case ListPlayersModule.Name:
                    return e.Name ?? "(未命名)";
                case ListPlayersModule.Character:
                    return e.Character ?? "未选角";
                case ListPlayersModule.Role:
                    if (e.IsHost && e.IsSelf) return "← 房主·你";
                    if (e.IsHost) return "← 房主";
                    if (e.IsSelf) return "← 你";
                    return "";
                case ListPlayersModule.Transport:
                    string transport = e.Relayed == null ? "unknown" : (e.Relayed.Value ? "relay" : "direct");
                    return $"[{transport}]";
                case ListPlayersModule.Ip:
                    return $"ip_src={(string.IsNullOrEmpty(e.RemoteIp) ? "—" : e.RemoteIp)}";
                case ListPlayersModule.SteamId:
                    return $"steam_id={(e.SteamId != 0 ? e.SteamId.ToString() : "未知")}";
                case ListPlayersModule.SteamName:
                    return $"steam_name={(string.IsNullOrEmpty(e.SteamName) ? "未知" : e.SteamName)}";
                case ListPlayersModule.Ping:
                    return $"ping={(e.PingMs >= 0 ? $"{e.PingMs}ms" : "—")}";
                default:
                    return "";
            }
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
                    characterId = e.CharacterId,
                    character = e.Character ?? "",
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
