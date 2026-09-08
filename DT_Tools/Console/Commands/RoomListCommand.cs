using System.Text;
using BepInEx.Logging;
using DummyClient;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /room_list
    ///
    /// 列出当前所有公开房间（含房间号、房主名、人数、状态、延迟）。
    ///
    /// 数据来源: SteamLobbyManager.RequestLobbyList()
    ///   - 仅返回公开房间（visibility=public），私密房间不可见
    ///   - 异步查询，调用后立即返回，结果稍后输出到控制台日志
    ///
    /// 这是纯读操作，不需要房主权限，在房间内也可查询。
    /// </summary>
    internal sealed class RoomListCommand : IConsoleCommand
    {
        public string   Name        => "room_list";
        public string[] Aliases     => new[] { "rooms", "list_rooms", "lobbies" };
        public string   Usage       => "room_list";
        public string   Description => "列出当前所有公开房间（异步查询）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            if (Managers.Network == null || Managers.Network.Lobby == null)
            {
                console.Log("网络管理器尚未初始化。", LogLevel.Warning);
                return;
            }

            console.Log("正在查询公开房间列表...", LogLevel.Info);

            Managers.Network.Lobby.RequestLobbyList(rooms =>
            {
                if (rooms == null || rooms.Count == 0)
                {
                    WebConsole.Instance?.Log("当前没有公开房间。", LogLevel.Info);
                    return;
                }

                var text = new StringBuilder();
                text.AppendLine($"━━━ 公开房间列表（{rooms.Count} 个） ━━━");

                int index = 1;
                foreach (var room in rooms)
                {
                    string state = room.State == "ingame" ? "游戏中" : "等待中";
                    string ping  = room.Ping >= 0 ? $"{room.Ping}ms" : "—";
                    string name  = string.IsNullOrEmpty(room.Name) ? "(未命名)" : room.Name;

                    text.AppendLine($"  {index,2}. {room.Code}  {name,-16} {room.Cur}/{room.Max}  {state}  {ping}");
                    index++;
                }

                WebConsole.Instance?.Log(text.ToString(), LogLevel.Info);
            });
        }
    }
}
