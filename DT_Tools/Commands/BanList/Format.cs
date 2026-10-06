using System.Collections.Generic;
using System.Linq;
using DT_Tools.Patches.System.BanList;

namespace DT_Tools.Commands.BanList
{
    /// <summary>/banlist /unban 输出格式化：人类回复文本 + JSON 结果 DTO。</summary>
    internal static class BanListFormat
    {
        /// <summary>条目显示名（无捕获记录时回退占位）。</summary>
        public static string DisplayName(BanListFeature.BanEntry e)
            => string.IsNullOrEmpty(e.Name) ? "(名字未知)" : e.Name;

        private static string Time(BanListFeature.BanEntry e)
            => e.KickedAt?.ToString("MM-dd HH:mm") ?? "无记录";

        /// <summary>封禁列表的人类可读文本，序号即 /unban 的 #序号。</summary>
        public static string List(List<BanListFeature.BanEntry> bans)
        {
            string lines = string.Join("\n",
                bans.Select((e, i) => $"  #{i + 1} {DisplayName(e)}  SteamId:{e.SteamId}  被踢于 {Time(e)}"));
            return $"当前被封禁 {bans.Count} 人：\n{lines}\n用 /unban #序号|SteamId|名字 解除封禁。";
        }

        public static object Item(BanListFeature.BanEntry e) => new
        {
            steamId = e.SteamId.ToString(),   // ulong 超出 JS 安全整数，按字符串下发（同 list_players）
            name = e.Name ?? "",
            time = Time(e),
        };

        public static object[] Items(List<BanListFeature.BanEntry> bans)
            => bans.Select(Item).ToArray();
    }
}
