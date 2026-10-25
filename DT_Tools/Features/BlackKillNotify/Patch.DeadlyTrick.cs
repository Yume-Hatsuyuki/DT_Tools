using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.BlackKillNotify
{
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.UseDeadlyTrick))]
    internal static class BlackKillNotifyDeadlyTrickPatch
    {
        private static void Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<BlackKillNotifyFeature>() || !BlackKillNotifyFeature.ClientSide)
                return;
            if (!__instance.IsDeadlyTrickReady
                || __instance.DeadlyTrickTarget == null
                || __instance.DeadlyTrickTarget.PublicInfo == null
                || !__instance.ServerCanAttack
                || __instance.Color != EPlayerColor.Black)
                return;
            Item weapon = __instance.Inventory != null ? __instance.Inventory.Weapon : null;
            if (weapon == null || weapon.DataId == 0)
                return;
            BlackKillNotifyLogic.RecordKillTarget(__instance.DeadlyTrickTarget.PublicInfo.PlayerId);
        }
    }
}
