using Data;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.BlackKillNotify
{
    /// <summary>
    /// MyPlayer.UseWeaponItem 前缀（public，0.1.16b MyPlayer.cs:704）：镜像其放行条件
    /// （CanAttack、Black、持刀、武器在册、目标在册）——盾牌拦截除外，被盾挡下不扣
    /// RemainKill 也不会发包，记录会被下一次真实击杀覆盖，不产生误报。放行即本帧
    /// 必然发出 C_KILL_PLAYER（0.1.16b 全客户端唯一发送点 MyPlayer.cs:743），先记下目标。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.UseWeaponItem))]
    internal static class BlackKillNotifyKnifeAttackPatch
    {
        private static void Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<BlackKillNotifyFeature>())
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
