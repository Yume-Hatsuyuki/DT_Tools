using System.Globalization;
using System.Text;
using BepInEx.Logging;
using DummyClient;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /room_list — 列出公开房间；同时 SetResult JSON 供 API / 自动化回调。
    /// </summary>
    internal sealed class RoomListCommand : IConsoleCommand
    {
        public string   Name        => "room_list";
        public string[] Aliases     => new[] { "rooms", "list_rooms", "lobbies" };
        public string   Usage       => "room_list";
        public string   Description => "列出当前所有公开房间（异步；日志 + JSON 回调）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            if (Managers.Network == null || Managers.Network.Lobby == null)
            {
                console.Log("网络管理器尚未初始化。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"network not ready\",\"rooms\":[]}");
                return;
            }

            console.Log("正在查询公开房间列表...", LogLevel.Info);

            Managers.Network.Lobby.RequestLobbyList(rooms =>
            {
                var wc = WebConsole.Instance;
                if (rooms == null || rooms.Count == 0)
                {
                    wc?.Log("当前没有公开房间。", LogLevel.Info);
                    wc?.SetResult("{\"ok\":true,\"count\":0,\"rooms\":[]}");
                    return;
                }

                var text = new StringBuilder();
                text.AppendLine($"━━━ 公开房间列表（{rooms.Count} 个） ━━━");

                var json = new StringBuilder();
                json.Append("{\"ok\":true,\"count\":").Append(rooms.Count).Append(",\"rooms\":[");

                int index = 1;
                foreach (var room in rooms)
                {
                    string state  = room.State == "ingame" ? "游戏中" : "等待中";
                    string ping   = room.Ping >= 0 ? $"{room.Ping}ms" : "—";
                    string name   = string.IsNullOrEmpty(room.Name) ? "(未命名)" : room.Name;
                    string lang   = string.IsNullOrEmpty(room.Lang) ? "—" : (room.Lang == "ANY" ? "任意" : room.Lang);
                    string region = string.IsNullOrEmpty(room.Region) ? "—" : room.Region;
                    string mic    = room.Mic == "on" ? "🔊" : "🔇";

                    text.AppendLine(
                        $"  {index,2}. {room.Code}  {name,-16} {room.Cur}/{room.Max}  {state}  {mic}  {region,-12} {lang}  {ping}");

                    if (index > 1) json.Append(',');
                    json.Append('{');
                    json.Append("\"code\":").Append(JsonStr(room.Code)).Append(',');
                    json.Append("\"name\":").Append(JsonStr(room.Name ?? "")).Append(',');
                    json.Append("\"players\":").Append(room.Cur).Append(',');
                    json.Append("\"max\":").Append(room.Max).Append(',');
                    json.Append("\"state\":").Append(JsonStr(room.State ?? "")).Append(',');
                    json.Append("\"mic\":").Append(JsonStr(room.Mic ?? "")).Append(',');
                    json.Append("\"lang\":").Append(JsonStr(room.Lang ?? "")).Append(',');
                    json.Append("\"region\":").Append(JsonStr(room.Region ?? "")).Append(',');
                    json.Append("\"ping\":").Append(room.Ping.ToString(CultureInfo.InvariantCulture));
                    json.Append('}');

                    index++;
                }

                json.Append("]}");

                wc?.Log(text.ToString(), LogLevel.Info);
                wc?.SetResult(json.ToString());
            });
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
