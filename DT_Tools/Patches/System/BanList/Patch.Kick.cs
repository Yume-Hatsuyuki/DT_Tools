using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Server.Game;
using Steamworks;

namespace DT_Tools.Patches.System.BanList
{
    /// <summary>
    /// KickPlayer 捕获（0.1.16b Server.Game/GameRoom.cs:1544）。
    /// Prefix 在玩家对象还留在房间时抓名字与 SteamId——Lobby 下踢人会同步经
    /// HandlePeerDisconnect → HandleLeavePlayer 把玩家移出房间（:1352/:1504），Postfix 里已查不到；
    /// Postfix 以游戏黑名单集合确认真被拉黑再落记录（门禁拒绝、无会话的目标不会进集合）。
    /// </summary>
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
