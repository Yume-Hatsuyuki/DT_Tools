using Data;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.BlackKillNotify
{
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.UseWeaponItem))]
    internal static class BlackKillNotifyKnifeAttackPatch
    {
        private static void Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<BlackKillNotifyFeature>() || !BlackKillNotifyFeature.ClientSide)
                return;
            if (!__instance.CanAttack || __instance.Color != EPlayerColor.Black)
                return;
            Item weapon = __instance.Inventory != null ? __instance.Inventory.Weapon : null;
            if (weapon == null || weapon.DataId == 0)
                return;
            if (Managers.Data == null
                || Managers.Data.ItemDic == null
                || !Managers.Data.ItemDic.TryGetValue(weapon.DataId, out ItemData data)
                || data == null
                || data.Type != EItemType.Weapon)
                return;
            Player target = __instance.AttackTargetPlayer;
            if (target == null || target.PublicInfo == null)
                return;
            BlackKillNotifyLogic.RecordKillTarget(target.PublicInfo.PlayerId);
        }
    }
}
