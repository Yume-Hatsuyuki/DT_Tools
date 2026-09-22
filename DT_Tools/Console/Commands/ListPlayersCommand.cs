using System.Linq;
using System.Text;
using BepInEx.Logging;
using Server.Game;
using Steamworks;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /list_players — 列出房间内玩家；同时 SetResult JSON。
    /// </summary>
    internal sealed class ListPlayersCommand : IConsoleCommand
    {
        public string   Name        => "list_players";
        public string[] Aliases     => new[] { "list", "who" };
        public string   Usage       => "list_players";
        public string   Description => "列出所有玩家的 PlayerId / SteamId / 昵称，并标注房主。";
        public string   Author      => "梦初雪";

        public bool RequireHost => false;

        public void Execute(string[] args, WebConsole console)
        {
            if (Managers.Player == null)
            {
                console.Log("玩家管理器尚未初始化（可能还未进入房间）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"player manager not ready\",\"count\":0,\"players\":[]}");
                return;
            }

            var players = Managers.Player.GetAllPlayers();
            if (players == null || players.Count == 0)
            {
                console.Log("当前没有已知玩家。", LogLevel.Warning);
                console.SetResult("{\"ok\":true,\"count\":0,\"hostId\":0,\"myId\":0,\"players\":[]}");
                return;
            }

            int hostId = ResolveHostPlayerId();
            int myId   = Managers.Player.MyPlayerID;

            var ordered = players
                .Where(p => p?.PublicInfo != null)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("━━━ 玩家列表 ━━━");

            var json = new StringBuilder();
            json.Append("{\"ok\":true");
            json.Append(",\"count\":").Append(ordered.Count);
            json.Append(",\"hostId\":").Append(hostId);
            json.Append(",\"myId\":").Append(myId);
            json.Append(",\"players\":[");

            int count = 0;
            foreach (var player in ordered)
            {
                int    pid     = player.PublicInfo.PlayerId;
                ulong  steamId = Managers.Player.GetRosterSteamId(pid);
                string name    = player.Name ?? "(未命名)";
                string steamStr = steamId != 0 ? steamId.ToString() : "未知";

                bool isHost = pid == hostId;
                bool isSelf = pid == myId;

                string tag = "";
                if (isHost && isSelf) tag = "  ← 房主·你";
                else if (isHost)      tag = "  ← 房主";
                else if (isSelf)      tag = "  ← 你";

                sb.AppendLine($"  #{pid,-4} {name,-16} steam_id={steamStr}{tag}");

                if (count > 0) json.Append(',');
                json.Append('{');
                json.Append("\"playerId\":").Append(pid).Append(',');
                json.Append("\"name\":").Append(JsonStr(name)).Append(',');
                json.Append("\"steamId\":").Append(JsonStr(steamId != 0 ? steamId.ToString() : "")).Append(',');
                json.Append("\"isHost\":").Append(isHost ? "true" : "false").Append(',');
                json.Append("\"isSelf\":").Append(isSelf ? "true" : "false");
                json.Append('}');

                count++;
            }

            if (hostId == 0)
                sb.AppendLine("（未能识别房主）");

            sb.Append($"共 {count} 名玩家");
            json.Append("]}");

            console.Log(sb.ToString(), LogLevel.Info);
            console.SetResult(json.ToString());
        }

        private static int ResolveHostPlayerId()
        {
            if (Managers.Host != null && Managers.Host.IsHost)
            {
                var host = GameRoom.Instance?.Host;
                if (host?.PublicInfo != null)
                    return host.PublicInfo.PlayerId;
            }

            var lobby = Managers.Network?.Lobby;
            if (lobby == null || !lobby.InLobby || lobby.HostSteamId == CSteamID.Nil)
                return 0;

            ulong hostSteam = lobby.HostSteamId.m_SteamID;
            if (hostSteam == 0)
                return 0;

            var players = Managers.Player.GetAllPlayers();
            if (players == null)
                return 0;

            foreach (var player in players)
            {
                if (player?.PublicInfo == null) continue;
                int pid = player.PublicInfo.PlayerId;
                if (Managers.Player.GetRosterSteamId(pid) == hostSteam)
                    return pid;
            }

            return 0;
        }

        private static string JsonStr(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
