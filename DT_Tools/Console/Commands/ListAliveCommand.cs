using System.Linq;
using System.Text;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /list_alive
    ///
    /// 列出当前仍存活的玩家，并标注阵营（黑幕 / 黑方 / 白方）与「你」。
    ///
    /// 权限 / 前置条件：仅房主；仅游戏中（选角结束之后）。
    /// 数据来源：
    ///   - GameRoom.Instance.Players
    ///   - Player.IsAlive / Color / IsSpectator / IsDummy
    ///   - Managers.Player.GetRosterSteamId
    ///
    /// 示例:
    ///   /list_alive
    ///   /alive
    /// </summary>
    internal sealed class ListAliveCommand : IConsoleCommand
    {
        public string   Name        => "list_alive";
        public string[] Aliases     => new[] { "alive", "存活", "存活人数" };
        public string   Usage       => "list_alive";
        public string   Description => "列出存活玩家并标注阵营（仅游戏中、需房主）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
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
                console.SetResult("{\"ok\":false,\"error\":\"not in game\",\"state\":\"" + room.State + "\"}");
                return;
            }

            if (Managers.Player == null)
            {
                console.Log("玩家管理器尚未初始化。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"player manager not ready\"}");
                return;
            }

            int myId = Managers.Player.MyPlayerID;

            // 用 var，避免 Server.Game.Player 与客户端 Player 类型冲突
            var alive = room.Players
                .Where(p => p?.PublicInfo != null
                    && !p.IsSpectator
                    && !p.IsDummy
                    && p.IsAlive)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

            int dark = 0, black = 0, white = 0, other = 0;

            var sb = new StringBuilder();
            sb.AppendLine("━━━ 存活玩家 ━━━");

            if (alive.Count == 0)
            {
                sb.Append("（无）");
                console.Log(sb.ToString(), LogLevel.Info);
            }
            else
            {
                foreach (var player in alive)
                {
                    int pid = player.PublicInfo.PlayerId;
                    ulong steamId = Managers.Player.GetRosterSteamId(pid);
                    string name = player.Name ?? "(未命名)";
                    string steamStr = steamId != 0 ? steamId.ToString() : "未知";

                    string faction = FactionLabel(player.Color);
                    switch (player.Color)
                    {
                        case EPlayerColor.Dark:  dark++;  break;
                        case EPlayerColor.Black: black++; break;
                        case EPlayerColor.White: white++; break;
                        default: other++; break;
                    }

                    string tag;
                    if (pid == myId && faction.Length > 0)
                        tag = $"  ← {faction}·你";
                    else if (pid == myId)
                        tag = "  ← 你";
                    else if (faction.Length > 0)
                        tag = $"  ← {faction}";
                    else
                        tag = "";

                    sb.AppendLine($"  #{pid,-4} {name,-16} steam_id={steamStr}{tag}");
                }

                sb.Append($"共 {alive.Count} 名存活");
                if (dark + black + white > 0)
                {
                    sb.Append("（");
                    bool first = true;
                    if (dark > 0)  { sb.Append($"黑幕 {dark}"); first = false; }
                    if (black > 0) { if (!first) sb.Append(" / "); sb.Append($"黑方 {black}"); first = false; }
                    if (white > 0) { if (!first) sb.Append(" / "); sb.Append($"白方 {white}"); }
                    if (other > 0) { if (!first) sb.Append(" / "); sb.Append($"其他 {other}"); }
                    sb.Append('）');
                }

                console.Log(sb.ToString(), LogLevel.Info);
            }

            // 结构化 JSON 供 /api/run 脚本使用
            var json = new StringBuilder();
            json.Append("{\"ok\":true,\"count\":").Append(alive.Count)
                .Append(",\"dark\":").Append(dark)
                .Append(",\"black\":").Append(black)
                .Append(",\"white\":").Append(white)
                .Append(",\"alive\":[");
            for (int i = 0; i < alive.Count; i++)
            {
                if (i > 0) json.Append(',');
                var p = alive[i];
                int pid = p.PublicInfo.PlayerId;
                ulong steamId = Managers.Player.GetRosterSteamId(pid);
                string name = p.Name ?? "";
                string factionKey = p.Color switch
                {
                    EPlayerColor.Dark  => "dark",
                    EPlayerColor.Black => "black",
                    EPlayerColor.White => "white",
                    _                  => "unknown"
                };
                json.Append("{\"pid\":").Append(pid)
                    .Append(",\"name\":").Append(JsonEscape(name))
                    .Append(",\"steam_id\":\"").Append(steamId).Append('"')
                    .Append(",\"faction\":\"").Append(factionKey).Append("\"}");
            }
            json.Append("]}");
            console.SetResult(json.ToString());
        }

        private static bool IsInGame(EGameState state)
        {
            return state != EGameState.Lobby
                && state != EGameState.NoneState
                && state != EGameState.PickCharacter;
        }

        private static string FactionLabel(EPlayerColor color)
        {
            return color switch
            {
                EPlayerColor.Dark  => "黑幕",
                EPlayerColor.Black => "黑方",
                EPlayerColor.White => "白方",
                _                  => ""
            };
        }

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
