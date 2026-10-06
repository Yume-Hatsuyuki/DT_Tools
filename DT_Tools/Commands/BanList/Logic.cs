using System;
using System.Collections.Generic;
using DT_Tools.Patches.System.BanList;

namespace DT_Tools.Commands.BanList
{
    /// <summary>/banlist /unban 业务：目标解析（#序号 / SteamId / 名字）。</summary>
    internal static class BanListLogic
    {
        /// <summary>/unban all：全部解封。</summary>
        public const string AllKeyword = "all";

        /// <summary>
        /// 把参数解析成封禁条目。#序号 = /banlist 列表里的序号；
        /// 纯数字 = SteamId（SteamID64 是 17 位长数字，不会与序号语义混淆）；其余按名字匹配。
        /// </summary>
        public static bool TryResolve(string arg, List<BanListFeature.BanEntry> bans,
            out BanListFeature.BanEntry target, out string error)
        {
            target = null;
            if (arg.StartsWith("#"))
            {
                if (!int.TryParse(arg.Substring(1), out int index) || index < 1 || index > bans.Count)
                {
                    error = $"无效的序号: {arg}（有效范围 1–{bans.Count}，可先执行 /banlist 查看）。";
                    return false;
                }
                target = bans[index - 1];
                error = null;
                return true;
            }
            if (ulong.TryParse(arg, out ulong steamId))
            {
                target = bans.Find(e => e.SteamId == steamId);
                if (target == null)
                {
                    error = $"SteamId {steamId} 不在封禁列表中。";
                    return false;
                }
                error = null;
                return true;
            }

            // 名字：精确匹配优先，其次唯一子串匹配（OrdinalIgnoreCase）
            List<BanListFeature.BanEntry> exact =
                bans.FindAll(e => string.Equals(e.Name, arg, StringComparison.OrdinalIgnoreCase));
            if (exact.Count == 1)
            {
                target = exact[0];
                error = null;
                return true;
            }
            if (exact.Count > 1)
            {
                error = $"有 {exact.Count} 个同名封禁玩家，请用 SteamId 或 #序号 指定。";
                return false;
            }
            List<BanListFeature.BanEntry> fuzzy =
                bans.FindAll(e => e.Name != null && e.Name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
            if (fuzzy.Count == 1)
            {
                target = fuzzy[0];
                error = null;
                return true;
            }
            if (fuzzy.Count > 1)
            {
                error = $"名字「{arg}」匹配到 {fuzzy.Count} 人，请用更完整的名字、SteamId 或 #序号。";
                return false;
            }
            error = $"封禁列表里找不到「{arg}」。";
            return false;
        }
    }
}
