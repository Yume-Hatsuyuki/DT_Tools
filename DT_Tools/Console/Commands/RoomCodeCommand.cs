using System.Text;
using BepInEx.Logging;
using DT_Tools.Features.System;
using Server.Game;
using Steamworks;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /room_code — 房间号与人数；同时 SetResult JSON。
    /// </summary>
    internal sealed class RoomCodeCommand : IConsoleCommand
    {
        public string   Name        => "room_code";
        public string[] Aliases     => new[] { "code", "room" };
        public string   Usage       => "room_code";
        public string   Description => "显示当前房间的房间号（邀请码）及玩家数。";
        public string   Author      => "梦初雪";

        public bool RequireHost => false;

        public void Execute(string[] args, WebConsole console)
        {
            if (Managers.Network == null)
            {
                console.Log("网络管理器尚未初始化。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"network not ready\"}");
                return;
            }

            string code = Managers.Network.RoomCode;

            if (string.IsNullOrEmpty(code))
            {
                console.Log("当前不在任何房间中（房间号为空）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"not in room\",\"code\":\"\"}");
                return;
            }

            int max = LobbyMaxPlayersFeature.MaxMembers;
            var lobby = Managers.Network.Lobby;

            int current;
            bool isHost = Managers.Host != null && Managers.Host.IsHost;
            if (isHost)
            {
                current = GameRoom.Instance?.Players.Count ?? 0;
            }
            else
            {
                current = (lobby != null && lobby.InLobby)
                    ? lobby.GetMembers().Count
                    : 0;
            }

            int steamLimit = 0;
            if (lobby != null && lobby.InLobby)
                steamLimit = SteamMatchmaking.GetLobbyMemberLimit(lobby.LobbyId);

            var text = new StringBuilder();
            text.AppendLine("━━━ 房间信息 ━━━");
            text.AppendLine($"  房间号:  {code}");
            text.Append($"  玩家数:  {current} / {max}");

            if (steamLimit > 0 && steamLimit != max)
                text.Append($"\n  Steam容器:  {current} / {steamLimit}（仅容器，进房仍按 {max}）");

            console.Log(text.ToString(), LogLevel.Info);

            var json = new StringBuilder();
            json.Append("{\"ok\":true");
            json.Append(",\"code\":").Append(JsonStr(code));
            json.Append(",\"players\":").Append(current);
            json.Append(",\"max\":").Append(max);
            json.Append(",\"isHost\":").Append(isHost ? "true" : "false");
            if (steamLimit > 0)
                json.Append(",\"steamLimit\":").Append(steamLimit);
            json.Append('}');
            console.SetResult(json.ToString());
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
