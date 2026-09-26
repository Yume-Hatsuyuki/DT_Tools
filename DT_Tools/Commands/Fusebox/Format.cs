using System.Collections.Generic;
using System.Text;
using DT_Tools.Game;

namespace DT_Tools.Commands.Fusebox
{
    /// <summary>/sabotage_fuse、/repair_fuse 列表文本格式化（设备查询结果 → 人类可读行）。</summary>
    internal static class FuseboxFormat
    {
        /// <summary>格式化电闸一行：#ID  房间  状态。</summary>
        public static string FormatLine(DeviceBase fusebox)
        {
            var (localized, _) = RoomLabel.FromDevice(fusebox);
            return $"  #{fusebox.ID}  房间: {localized}  状态: {FuseboxLogic.GetStateLabel(fusebox)}";
        }

        /// <summary>无参 /sabotage_fuse 的已武装电闸列表。</summary>
        public static string ArmedListReply(List<DeviceBase> armed)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"已武装且完好的电闸共 {armed.Count} 个（MissionType==-1, StateList[0]==0）：");
            foreach (var fb in armed)
                sb.AppendLine(FormatLine(fb));
            sb.Append("用法: /sabotage_fuse all 拉断全部 | /sabotage_fuse #<id> 拉断单个");
            return sb.ToString();
        }

        /// <summary>无参 /repair_fuse 的损坏电闸列表。</summary>
        public static string BrokenListReply(List<DeviceBase> broken)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"损坏的电闸共 {broken.Count} 个（StateList[0]==9999）：");
            foreach (var fb in broken)
                sb.AppendLine(FormatLine(fb));
            sb.Append("用法: /repair_fuse all 秒修全部 | /repair_fuse #<id> 秒修单个");
            return sb.ToString();
        }
    }
}
