using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// <summary>
    /// 原版 SpawnNextWeapon(bool isInit)（0.1.15b DeviceManager.cs:509）只开放 CurrentArmory 一架；
    /// 刷刀后按 WeaponCount 补足 / 收敛开放架数（CurrentArmory 恒保留）。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.DeviceManager), nameof(Server.Game.DeviceManager.SpawnNextWeapon))]
    internal static class MultiArmoryWeaponSpawnNextWeaponPatch
    {
        private static void Postfix(Server.Game.DeviceManager __instance, bool isInit)
        {
            if (!Engine.Enabled<MultiArmoryWeaponFeature>())
                return;

            MultiArmoryWeaponLogic.OpenAfterSpawn(__instance, isInit);
        }
    }
}
