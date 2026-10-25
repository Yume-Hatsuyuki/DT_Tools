using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DT_Tools.Commands.Modded
{
    internal static class ModdedFormat
    {
        public static string Help() =>
            "用法: /modded [all|#id] — 列出玩家及其 modded（改装检测）标记。\n" +
            "  /modded       → 帮助\n" +
            "  /modded all   → 全部玩家\n" +
            "  /modded #3    → 指定玩家\n" +
            "标记: ✔=modded  ✖=未改装  ?=版本不支持（该游戏版本没有 modded 字段）。\n" +
            "数据: 房主读本机房间；客户端读房主广播的快照（约每秒刷新）。\n" +
            "注意: 标记由玩家进房时自报，未经核验；开了 ModDetectorNeutralize 的 DT_Tools 玩家会上报 ✖，本命令主要揭示未中和的改装玩家。";

        public static string Reply(ModdedLogic.CollectResult result, int myId)
        {
            var players = result.Players;
            var sb = new StringBuilder();
            sb.AppendLine("━━━ modded 标记 ━━━");
            if (players.Count == 0)
            {
                sb.AppendLine("（无玩家）");
            }
            else
            {
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
                sb.AppendLine($"共 {players.Count} 名");
            }
            sb.Append(SourceNote(result.Source));
            return sb.ToString();
        }

        public static object Result(ModdedLogic.CollectResult result, int myId) => new
        {
            source = result.Source == ModdedLogic.CollectSource.Host ? "host" : "snapshot",
            supported = result.Players.Any(p => p.Modded != null),
            count = result.Players.Count,
            myId = myId,
            players = result.Players.Select(p => new
            {
                pid = p.Pid,
                name = p.Name ?? "",
                is_host = p.IsHost,
                is_self = p.IsSelf,
                modded = p.Modded,
            }).ToList(),
        };

        private static string SourceNote(ModdedLogic.CollectSource source) => source switch
        {
            ModdedLogic.CollectSource.Host     => "数据来源：本机房间。标记为玩家进房时自报，未经核验。",
            ModdedLogic.CollectSource.Snapshot => "数据来源：房主广播的快照（约每秒刷新）。标记为玩家进房时自报，未经核验。",
            _ => "",
        };
    }
}
