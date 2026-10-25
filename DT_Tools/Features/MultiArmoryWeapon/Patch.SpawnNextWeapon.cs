using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.MultiArmoryWeapon
{
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
