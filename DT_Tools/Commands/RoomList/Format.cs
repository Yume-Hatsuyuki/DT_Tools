using System.Collections.Generic;
using System.Text;
using DummyClient;

namespace DT_Tools.Commands.RoomList
{
    /// <summary>
    /// /room_list 输出格式化：公开房间列表人类文本。查询是异步回调，
    /// 结果统一经 ctx.Reply 走日志通道输出（无独立 JSON DTO——异步命令的机器
    /// 结果在 Command.Execute 里即时返回 "已受理"，见 Command.cs）。
    /// </summary>
    internal static class RoomListFormat
    {
        public static string Reply(List<LobbyListEntry> rooms)
        {
            var text = new StringBuilder();
            text.AppendLine($"━━━ 公开房间列表（{rooms.Count} 个） ━━━");

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
                index++;
            }

            return text.ToString();
        }
    }
}
