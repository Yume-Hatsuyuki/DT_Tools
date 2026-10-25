using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.SilentWeaponMove
{
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.AlertMessage))]
    internal static class SilentWeaponMoveAlertPatch
    {
        private static bool Prefix(Player player, ESystemMessageType type)
        {
            if (!Engine.Enabled<SilentWeaponMoveFeature>())
                return true;
            if (type != ESystemMessageType.WeaponMoved)
                return true;

            Log.Info<SilentWeaponMoveFeature>($"已抑制刀转移通知（{player?.Name ?? "?"}）");
            return false;
        }
    }
}
