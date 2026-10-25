using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Server.Game;
using Steamworks;

namespace DT_Tools.Features.BanList
{
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.KickPlayer))]
    internal static class BanListKickPatch
    {
        private static bool _pending;
        private static CSteamID _pendingSteamId;
        private static string _pendingName;

        private static void Prefix(GameRoom __instance, int targetId)
        {
            _pending = false;
            if (!Engine.Enabled<BanListFeature>())
                return;
            Server.Game.Player target =
                __instance.Players.FirstOrDefault(p => p.PublicInfo.PlayerId == targetId);
            if (target?.Session == null)
                return;
            _pendingSteamId = target.Session.SteamId;
            _pendingName = target.Name;
            _pending = true;
        }

        private static void Postfix()
        {
            if (!_pending)
                return;
            _pending = false;
            if (BanListFeature.Contains(_pendingSteamId))
                BanListFeature.Record(_pendingSteamId.m_SteamID, _pendingName);
        }
    }
}
