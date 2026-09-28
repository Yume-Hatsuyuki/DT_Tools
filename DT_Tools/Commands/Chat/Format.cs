using System.Collections.Generic;
using System.Linq;
using System.Text;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.Chat
{
    /// <summary>/chat 输出格式化：帮助列表 + 发送结果 DTO。</summary>
    internal static class ChatFormat
    {
        public static string HelpAndList(List<DeviceBase> devices)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 打字机 /chat ━━━");
            sb.AppendLine("通过地图打字机发送全图文字（仅 Survive + 存活）。");
            sb.AppendLine();
            sb.AppendLine("用法：");
            sb.AppendLine("  /chat                  本帮助 + 列出全部打字机");
            sb.AppendLine("  /chat #id 文本         指定打字机 ID 发送");
            sb.AppendLine("  /chat 文本             随机空闲打字机发送");
            sb.AppendLine();
            sb.AppendLine("示例：");
            sb.AppendLine("  /chat #12 食堂集合");
            sb.AppendLine("  /chat 有人看到刀了吗");
            sb.AppendLine();

            if (devices == null || devices.Count == 0)
            {
                sb.AppendLine("【打字机列表】（空）— 地图未加载 / 不在对局中");
                return sb.ToString().TrimEnd();
            }

            sb.AppendLine($"【打字机列表】共 {devices.Count} 台（ID 可用于 /chat #id 文本）");
            foreach (var d in devices)
            {
                var (loc, raw) = RoomLabel.FromDevice(d);
                string occ = OccupancyLabel(d.DeviceState);
                var pos = d.Info?.Pos;
                string posText = pos != null ? $"({pos.X:F0}, {pos.Y:F0})" : "(-, -)";
                sb.AppendLine($"  #{d.ID,-4}  {loc}（{raw}）  {posText}  {occ}");
            }
            return sb.ToString().TrimEnd();
        }

        public static object ListResult(List<DeviceBase> devices)
        {
            return new
            {
                mode = "list",
                devices = (devices ?? new List<DeviceBase>()).Select(d =>
                {
                    var (loc, raw) = RoomLabel.FromDevice(d);
                    var pos = d.Info?.Pos;
                    return new
                    {
                        id = d.ID,
                        room = raw,
                        roomLocalized = loc,
                        x = pos?.X ?? 0f,
                        y = pos?.Y ?? 0f,
                        occupiedBy = d.DeviceState,
                    };
                }),
            };
        }

        public static object SendResult(DeviceBase device, string roomRaw, string text)
        {
            return new
            {
                mode = "send",
                deviceId = device.ID,
                room = roomRaw,
                text,
            };
        }

        /// <summary>DeviceState：0=空闲，非 0=占用者 PlayerId。</summary>
        private static string OccupancyLabel(int state)
        {
            if (state == 0) return "空闲";
            return $"占用中(#{state})";
        }
    }
}
