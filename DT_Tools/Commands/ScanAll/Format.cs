using System.Collections.Generic;
using System.Linq;
using System.Text;
using Protocol;

namespace DT_Tools.Commands.ScanAll
{
    /// <summary>/scan_all 输出格式化：扫描清单文本 + JSON 结果 DTO。</summary>
    internal static class ScanAllFormat
    {
        /// <summary>设备分类中文标签（尸体 / 武器架 / 其他设备）。</summary>
        public static string Category(EDeviceType type)
            => type switch
            {
                EDeviceType.Corpse => "尸体",
                EDeviceType.Armory => "武器架",
                _                  => "设备",
            };

        public static string Reply(List<DeviceBase> targets)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【知晓一切】已对以下可扫描设备发送 C_SCAN_DEVICE：");

            foreach (var d in targets)
                sb.AppendLine($"  #{d.ID,-4} {d.DeviceType,-16} ({Category(d.DeviceType)})");

            int corpse = targets.Count(d => d.DeviceType == EDeviceType.Corpse);
            int armory = targets.Count(d => d.DeviceType == EDeviceType.Armory);
            int other = targets.Count - corpse - armory;
            sb.AppendLine($"共 {targets.Count} 个（尸体 {corpse} / 武器架 {armory} / 其他 {other}）。");
            sb.Append("服务端无成功回执：线索会陆续通过 S_SCAN_DEVICE / S_SCAN_CORPSE / S_SCAN_ARMORY 推送到平板。");
            return sb.ToString();
        }

        public static object Result(List<DeviceBase> targets)
            => new
            {
                scanned = targets.Select(d => new
                {
                    id = d.ID,
                    type = d.DeviceType.ToString(),
                    category = Category(d.DeviceType),
                }),
                total = targets.Count,
                corpse = targets.Count(d => d.DeviceType == EDeviceType.Corpse),
                armory = targets.Count(d => d.DeviceType == EDeviceType.Armory),
                other = targets.Count(d => d.DeviceType != EDeviceType.Corpse && d.DeviceType != EDeviceType.Armory),
            };
    }
}
