// 作用：按名册回收已取消隐藏的身份标记；不改变原生名册消费与玩家增删逻辑。
using System.Collections.Generic;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.HideIdentityLive
{
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ApplyRoster))]
    internal static class HideIdentityRosterPatch
    {
        private static readonly FieldInfo HiddenIds =
            AccessTools.Field(typeof(PlayerManager), "_hiddenIdentityIds");

        private static readonly FieldInfo HiddenSteamIds =
            AccessTools.Field(typeof(PlayerManager), "_hiddenSteamIds");

        private static void Postfix(S_PLAYER_ROSTER pkt)
        {
            if (!Engine.Enabled<HideIdentityLiveFeature>() || pkt == null || Managers.Player == null)
                return;

            var hiddenIds = HiddenIds?.GetValue(Managers.Player) as HashSet<int>;
            var hiddenSteamIds = HiddenSteamIds?.GetValue(Managers.Player) as HashSet<ulong>;
            if (hiddenIds == null || hiddenSteamIds == null)
                return;

            foreach (RosterEntry entry in pkt.Entries)
            {
                if (entry.HideIdentity)
                    continue;

                hiddenIds.Remove(entry.PlayerId);
                ulong steamId = Managers.Player.GetRosterSteamId(entry.PlayerId);
                if (steamId == 0L || IsSteamIdStillHidden(pkt, steamId))
                    continue;
                hiddenSteamIds.Remove(steamId);
            }
        }

        private static bool IsSteamIdStillHidden(S_PLAYER_ROSTER pkt, ulong steamId)
        {
            foreach (RosterEntry entry in pkt.Entries)
            {
                if (entry.HideIdentity && Managers.Player.GetRosterSteamId(entry.PlayerId) == steamId)
                    return true;
            }
            return false;
        }
    }
}
