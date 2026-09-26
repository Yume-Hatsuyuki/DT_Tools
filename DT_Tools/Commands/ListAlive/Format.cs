using System.Collections.Generic;
using System.Linq;
using System.Text;
using Protocol;

namespace DT_Tools.Commands.ListAlive
{
    /// <summary>/list_alive 输出格式化：阵营标注列表 + JSON 结果 DTO。</summary>
    internal static class ListAliveFormat
    {
        public static string FactionLabel(EPlayerColor color)
            => color switch
            {
                EPlayerColor.Dark  => "黑幕",
                EPlayerColor.Black => "黑方",
                EPlayerColor.White => "白方",
                _                  => "",
            };

        private static string FactionKey(EPlayerColor color)
            => color switch
            {
                EPlayerColor.Dark  => "dark",
                EPlayerColor.Black => "black",
                EPlayerColor.White => "white",
                _                  => "unknown",
            };

        public static string Reply(List<ListAliveLogic.Entry> alive, int myId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 存活玩家 ━━━");

            if (alive.Count == 0)
            {
                sb.Append("（无）");
                return sb.ToString();
            }

            foreach (var e in alive)
            {
                string steamStr = e.SteamId != 0 ? e.SteamId.ToString() : "未知";
                string faction = FactionLabel(e.Color);

                string tag;
                if (e.Pid == myId && faction.Length > 0)
                    tag = $"  ← {faction}·你";
                else if (e.Pid == myId)
                    tag = "  ← 你";
                else if (faction.Length > 0)
                    tag = $"  ← {faction}";
                else
                    tag = "";

                sb.AppendLine($"  #{e.Pid,-4} {e.Name,-16} steam_id={steamStr}{tag}");
            }

            sb.Append($"共 {alive.Count} 名存活");
            sb.Append(CountSummary(alive));
            return sb.ToString();
        }

        private static string CountSummary(List<ListAliveLogic.Entry> alive)
        {
            int dark = alive.Count(e => e.Color == EPlayerColor.Dark);
            int black = alive.Count(e => e.Color == EPlayerColor.Black);
            int white = alive.Count(e => e.Color == EPlayerColor.White);
            int other = alive.Count - dark - black - white;

            if (dark + black + white <= 0) return "";

            var sb = new StringBuilder("（");
            bool first = true;
            if (dark > 0)  { sb.Append($"黑幕 {dark}"); first = false; }
            if (black > 0) { if (!first) sb.Append(" / "); sb.Append($"黑方 {black}"); first = false; }
            if (white > 0) { if (!first) sb.Append(" / "); sb.Append($"白方 {white}"); first = false; }
            if (other > 0) { if (!first) sb.Append(" / "); sb.Append($"其他 {other}"); }
            sb.Append('）');
            return sb.ToString();
        }

        public static object Result(List<ListAliveLogic.Entry> alive)
            => new
            {
                count = alive.Count,
                dark = alive.Count(e => e.Color == EPlayerColor.Dark),
                black = alive.Count(e => e.Color == EPlayerColor.Black),
                white = alive.Count(e => e.Color == EPlayerColor.White),
                alive = alive.Select(e => new
                {
                    pid = e.Pid,
                    name = e.Name ?? "",
                    steam_id = e.SteamId.ToString(),
                    faction = FactionKey(e.Color),
                }),
            };
    }
}
