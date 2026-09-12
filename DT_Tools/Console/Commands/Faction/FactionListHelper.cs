using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands.Faction
{
    /// <summary>
    /// 黑方 / 白方 / 黑幕 列表共用：仅房主、仅游戏中，按 PlayerId 升序输出。
    /// 同时通过 console.SetResult 提供结构化 JSON，方便脚本消费 /api/run 返回值。
    /// </summary>
    internal static class FactionListHelper
    {
        /// <summary>
        /// 非大厅的进行中状态（选角结束之后）。
        /// </summary>
        private static bool IsInGame(EGameState state)
        {
            return state != EGameState.Lobby
                && state != EGameState.NoneState
                && state != EGameState.PickCharacter;
        }

        public static void Execute(WebConsole console, EPlayerColor color, string title)
        {
            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行（完整阵营数据仅在 Host 端）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"host only\"}");
                return;
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                console.Log("当前没有活动的游戏房间。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no room\"}");
                return;
            }

            if (!IsInGame(room.State))
            {
                console.Log($"仅在游戏中可用（当前状态: {room.State}）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"not in game\",\"state\":" + JsonEscape(room.State.ToString()) + "}");
                return;
            }

            if (Managers.Player == null)
            {
                console.Log("玩家管理器尚未初始化。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"player manager not ready\"}");
                return;
            }

            int myId = Managers.Player.MyPlayerID;

            // 用 var：程序集里另有客户端 Player，裸写 List<Player> 会和 Server.Game.Player 冲突（CS0029）
            var matched = room.Players
                .Where(p => p?.PublicInfo != null
                    && !p.IsSpectator
                    && !p.IsDummy
                    && p.Color == color)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

            // ── 文本日志（WebUI / BepInEx） ──────────────────
            var sb = new StringBuilder();
            sb.AppendLine($"━━━ {title} ━━━");

            if (matched.Count == 0)
            {
                sb.Append("（无）");
                console.Log(sb.ToString(), LogLevel.Info);
            }
            else
            {
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
                console.Log(sb.ToString(), LogLevel.Info);
            }

            // ── 结构化 JSON（/api/run 脚本用） ───────────────
            // 键名与 color 对应：dark / white / black
            string key = color switch
            {
                EPlayerColor.Dark  => "dark",
                EPlayerColor.White => "white",
                EPlayerColor.Black => "black",
                _                  => "players"
            };

            var json = new StringBuilder();
            json.Append("{\"ok\":true,\"").Append(key).Append("\":[");
            for (int i = 0; i < matched.Count; i++)
            {
                if (i > 0) json.Append(',');
                var p = matched[i];
                int pid = p.PublicInfo.PlayerId;
                ulong steamId = Managers.Player.GetRosterSteamId(pid);
                string name = p.Name ?? "";
                json.Append("{\"pid\":").Append(pid)
                    .Append(",\"name\":").Append(JsonEscape(name))
                    .Append(",\"steam_id\":\"").Append(steamId).Append("\"}");
            }
            json.Append("]}");
            console.SetResult(json.ToString());
        }

        // 本地轻量转义，避免依赖 WebConsole 的 private JsonEscape
        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
