using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Core.Attributes;
using HarmonyLib;
using Server.Game;
using Steamworks;

namespace DT_Tools.Patches.System.BanList
{
    /// <summary>
    /// 踢人黑名单：房主踢人时游戏把对方 SteamId 记进 GameRoom._bannedSteamIds
    /// （0.1.16b Server.Game/GameRoom.cs:79），此后进房校验直接拒绝
    /// （HandleEnterPlayer，:1068，回包 ErrorKicked），且原版没有任何解封接口——
    /// 房主会话存续期间被踢者永远进不来，只有 FullReset（:522）才清空。
    ///
    /// 本功能不改游戏行为，只做两件事：KickPlayer 补丁把「被踢时的名字与时间」缓存下来
    /// （黑名单本体仍是游戏自己的集合，本表只补名字，游戏侧未拉黑就以游戏为准）；
    /// 对命令域暴露快照（/banlist）与解封操作（/unban）。
    /// </summary>
    [PatchFeature(
        "踢人黑名单：记录被踢玩家（名字/时间），供 /banlist 查询与 /unban 解封。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class BanListFeature
    {
        // 私有字段锚点：0.1.16b Server.Game/GameRoom.cs:79（升级时全文搜索 _bannedSteamIds 核对）
        private static readonly FieldInfo BannedField =
            AccessTools.Field(typeof(GameRoom), "_bannedSteamIds");

        /// <summary>名字缓存：SteamId → (名字, 被踢时间)。真值以游戏黑名单集合为准，本表只补展示信息。</summary>
        private static readonly Dictionary<ulong, (string Name, DateTime Time)> Records = new();

        /// <summary>黑名单条目快照。Name / KickedAt 为 null 表示该 SteamId 在游戏黑名单里但没有捕获记录（旧封禁）。</summary>
        public sealed class BanEntry
        {
            public ulong SteamId;
            public string Name;
            public DateTime? KickedAt;
        }

        /// <summary>游戏黑名单是否已含该 SteamId（Patch.Kick 后缀确认拉黑用）。</summary>
        internal static bool Contains(CSteamID steamId)
        {
            HashSet<CSteamID> set = ReadSet();
            return set != null && set.Contains(steamId);
        }

        /// <summary>记录被踢者的名字与时间（Patch.Kick 确认游戏侧已拉黑后调用）。重复被踢覆盖旧记录。</summary>
        public static void Record(ulong steamId, string name)
            => Records[steamId] = (name, DateTime.Now);

        /// <summary>
        /// 黑名单快照（确定性排序：有捕获记录的按被踢时间升序，无记录的按 SteamId 升序垫底）。
        /// 顺带清理已不在黑名单里的过期名字缓存——房间 FullReset 清空游戏集合后本表自动自愈。
        /// </summary>
        public static List<BanEntry> Snapshot()
        {
            HashSet<CSteamID> set = ReadSet();
            if (set == null || set.Count == 0)
            {
                Records.Clear();
                return new List<BanEntry>();
            }
            foreach (ulong id in Records.Keys.Where(id => !set.Contains(new CSteamID(id))).ToList())
                Records.Remove(id);
            List<BanEntry> list = Records
                .Where(kv => set.Contains(new CSteamID(kv.Key)))
                .OrderBy(kv => kv.Value.Time)
                .Select(kv => new BanEntry { SteamId = kv.Key, Name = kv.Value.Name, KickedAt = kv.Value.Time })
                .ToList();
            list.AddRange(set
                .Where(id => !Records.ContainsKey(id.m_SteamID))
                .Select(id => new BanEntry { SteamId = id.m_SteamID })
                .OrderBy(e => e.SteamId));
            return list;
        }

        /// <summary>解除封禁：从游戏黑名单移除，名字缓存一并清掉。返回是否确实在黑名单里。</summary>
        public static bool TryUnban(ulong steamId)
        {
            HashSet<CSteamID> set = ReadSet();
            if (set == null || !set.Remove(new CSteamID(steamId)))
                return false;
            Records.Remove(steamId);
            return true;
        }

        private static HashSet<CSteamID> ReadSet()
            => BannedField?.GetValue(GameRoom.Instance) as HashSet<CSteamID>;
    }
}
