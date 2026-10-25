using System.Collections.Generic;
using System.Linq;
using DT_Tools.Features.BanList;

namespace DT_Tools.Commands.BanList
{
    internal static class BanListFormat
    {
        public static string DisplayName(BanListFeature.BanEntry e)
            => string.IsNullOrEmpty(e.Name) ? "(名字未知)" : e.Name;

        private static string Time(BanListFeature.BanEntry e)
            => e.KickedAt?.ToString("MM-dd HH:mm") ?? "无记录";

        public static string List(List<BanListFeature.BanEntry> bans)
        {
            string lines = string.Join("\n",
                bans.Select((e, i) => $"  #{i + 1} {DisplayName(e)}  SteamId:{e.SteamId}  被踢于 {Time(e)}"));
            return $"当前被封禁 {bans.Count} 人：\n{lines}\n用 /unban #序号|SteamId|名字 解除封禁。";
        }

        public static object Item(BanListFeature.BanEntry e) => new
        {
            steamId = e.SteamId.ToString(),
            name = e.Name ?? "",
            time = Time(e),
        };

        public static object[] Items(List<BanListFeature.BanEntry> bans)
            => bans.Select(Item).ToArray();
    }
}
