using System.Collections.Generic;
using System.Linq;
using System.Text;
using Protocol;

namespace DT_Tools.Commands.Faction
{
    internal static class FactionFormat
    {
        public static string Reply(string title, List<Server.Game.Player> matched, int myId)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"━━━ {title} ━━━");

            if (matched.Count == 0)
            {
                sb.Append("（无）");
                return sb.ToString();
            }

            foreach (var player in matched)
            {
                int pid = player.PublicInfo.PlayerId;
                ulong steamId = Managers.Player.GetRosterSteamId(pid);
                string name = player.Name ?? "(未命名)";
                string steamStr = steamId != 0 ? steamId.ToString() : "未知";
                string tag = pid == myId ? "  ← 你" : "";

                sb.AppendLine($"  #{pid,-4} {name,-16} steam_id={steamStr}{tag}");
            }

            sb.Append($"共 {matched.Count} 名");
            return sb.ToString();
        }

        public static object Result(EPlayerColor color, List<Server.Game.Player> matched)
        {
            string key = color switch
            {
                EPlayerColor.Dark  => "dark",
                EPlayerColor.White => "white",
                EPlayerColor.Black => "black",
                _                  => "players"
            };

            var items = matched
                .Select(p => new
                {
                    pid = p.PublicInfo.PlayerId,
                    name = p.Name ?? "",
                    steam_id = Managers.Player.GetRosterSteamId(p.PublicInfo.PlayerId).ToString(),
                })
                .ToList();

            var data = new Dictionary<string, object> { [key] = items };
            return data;
        }
    }
}
