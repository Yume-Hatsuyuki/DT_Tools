using System.Collections.Generic;
using System.Linq;
using System.Text;
using Protocol;

namespace DT_Tools.Commands.ReportCorpse
{
    /// <summary>/report 输出格式化：尸体列表文本 + JSON 结果 DTO。</summary>
    internal static class ReportCorpseFormat
    {
        public static string ReplyList(List<DeviceBase> corpses, DeviceManager device)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 场上尸体 ━━━");

            if (corpses.Count == 0)
            {
                sb.AppendLine("（场上没有尸体 — 还没人死）");
                sb.AppendLine();
                sb.Append("用法: /report #<corpseId>  报警指定尸体强制开会");
                return sb.ToString();
            }

            int reportable = 0;
            foreach (var d in corpses)
            {
                string name = ReportCorpseLogic.FindPlayerName(d.ID);
                string block = ReportCorpseLogic.GetBlockReason(device, d);
                if (block == null) reportable++;
                string status = block == null ? "可报警" : ("不可报警·" + block);
                sb.AppendLine($"  #{d.ID,-4} {name,-16}  ← {status}");
            }

            sb.AppendLine($"共 {corpses.Count} 具尸体（可报警 {reportable} 具）。");
            sb.AppendLine();
            sb.Append(reportable > 0
                ? "用法: /report #<corpseId>  报警指定尸体强制开会"
                : "当前没有可报警的尸体（全部被隐藏 / 锁定 / 炸弹尸体）。");
            return sb.ToString();
        }

        public static string ReplyReport(DeviceBase corpse)
        {
            string corpseName = ReportCorpseLogic.FindPlayerName(corpse.ID);
            return $"【开庭】已发送 C_INTERACT_CORPSE：报警尸体 #{corpse.ID}（{corpseName}），" +
                   "无视距离/身份/存活状态，等待服务端 EndSurvival 切换到调查阶段。";
        }

        public static object ResultList(List<DeviceBase> corpses, DeviceManager device)
            => new
            {
                corpses = corpses.Select(d =>
                {
                    string block = ReportCorpseLogic.GetBlockReason(device, d);
                    return new
                    {
                        corpseId = d.ID,
                        name = ReportCorpseLogic.FindPlayerName(d.ID),
                        reportable = block == null,
                        reason = block,
                    };
                }),
                total = corpses.Count,
                reportable = corpses.Count(d => ReportCorpseLogic.GetBlockReason(device, d) == null),
            };

        public static object ResultReport(DeviceBase corpse)
            => new { corpseId = corpse.ID };
    }
}
