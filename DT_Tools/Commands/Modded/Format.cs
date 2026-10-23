using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DT_Tools.Commands.Modded
{
    /// <summary>
    /// /modded 输出格式化：人类回复文本 + JSON 结果 DTO。
    /// </summary>
    internal static class ModdedFormat
    {
        public static string Help() =>
            "用法: /modded [all|#id] — 列出玩家及其 modded（改装检测）标记。\n" +
            "  /modded       → 帮助\n" +
            "  /modded all   → 全部玩家\n" +
            "  /modded #3    → 指定玩家\n" +
            "标记: ✔=modded  ✖=未改装  ?=版本不支持（0.1.20a 无 IsModded）。\n" +
            "注意: 开了 ModDetectorNeutralize 的 DT_Tools 玩家会上报 ✖，本命令主要揭示未中和的改装玩家。";

        public static string Reply(List<ModdedLogic.Entry> players, int myId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ modded 标记 ━━━");
            if (players.Count == 0)
            {
                sb.Append("（无玩家）");
                return sb.ToString();
            }

            foreach (var p in players)
            {
                string mark = p.Modded switch
                {
                    true  => "✔ modded",
                    false => "✖ 未改装",
                    null  => "? 版本不支持",
                };
                string tag = p.IsHost ? "  [房主]" : "";
                tag += p.IsSelf ? "  ← 你" : "";
                sb.AppendLine($"  #{p.Pid,-4} {p.Name,-16} {mark}{tag}");
            }

            sb.Append($"共 {players.Count} 名");
            return sb.ToString();
        }

        public static object Result(List<ModdedLogic.Entry> players, int myId) => new
        {
            supported = players.Any(p => p.Modded != null),
            count = players.Count,
            myId = myId,
            players = players.Select(p => new
            {
                pid = p.Pid,
                name = p.Name ?? "",
                is_host = p.IsHost,
                is_self = p.IsSelf,
                modded = p.Modded,
            }).ToList(),
        };
    }
}
