using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Core.Attributes;
using HarmonyLib;
using Server.Game;
using Steamworks;

namespace DT_Tools.Features.BanList
{
    [PatchFeature(
        "踢人黑名单：记录被踢玩家（名字/时间），供 /banlist 查询与 /unban 解封。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.SysHost,
        Author = "梦初雪")]
    public sealed class BanListFeature
    {
        private static readonly FieldInfo BannedField =
            AccessTools.Field(typeof(GameRoom), "_bannedSteamIds");

        private static readonly Dictionary<ulong, (string Name, DateTime Time)> Records = new();

        public sealed class BanEntry
        {
            public ulong SteamId;
            public string Name;
            public DateTime? KickedAt;
        }

        internal static bool Contains(CSteamID steamId)
        {
            HashSet<CSteamID> set = ReadSet();
            return set != null && set.Contains(steamId);
        }

        public static void Record(ulong steamId, string name)
            => Records[steamId] = (name, DateTime.Now);

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
