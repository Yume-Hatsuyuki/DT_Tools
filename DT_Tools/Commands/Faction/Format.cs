using System.Collections.Generic;
using System.Linq;
using System.Text;
using Protocol;

namespace DT_Tools.Commands.Faction
{
    /// <summary>/list_* 阵营列表输出格式化：人类回复文本 + JSON 结果 DTO（键名沿用旧 JSON）。</summary>
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

        /// <summary>
        /// JSON 结果（键名沿用旧实现）：顶层键与 color 对应 dark / white / black，
        /// 元素 {pid, name, steam_id}（steam_id 保持字符串形状）。
        /// </summary>
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

            // 匿名对象无法动态键名，改用字典承载（Newtonsoft 序列化形状一致）
            var data = new Dictionary<string, object> { [key] = items };
            return data;
        }
    }
}
