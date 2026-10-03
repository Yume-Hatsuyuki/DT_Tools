using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.BlackKillNotify
{
    /// <summary>
    /// MyPlayer.UseDeadlyTrick 前缀（public，0.1.16b MyPlayer.cs:751）：镜像其放行条件
    /// （IsDeadlyTrickReady、目标在册、ServerCanAttack、Black、持刀）——放行即本帧
    /// 必然发出 C_DEADLY_TRICK，先记下目标。必须在前缀记录：原方法发送后立即把
    /// DeadlyTrickTarget 置空（MyPlayer.cs:772），后缀读不到。处决技击杀与持刀共用
    /// 服务端 ConsumeKillAndRearm 扣减（0.1.16b Server.Game/Player.cs:1261），
    /// RemainKill 下降同样触发通报。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.UseDeadlyTrick))]
    internal static class BlackKillNotifyDeadlyTrickPatch
    {
        private static void Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<BlackKillNotifyFeature>())
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
